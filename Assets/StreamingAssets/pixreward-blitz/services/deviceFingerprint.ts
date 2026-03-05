/**
 * Device Fingerprint Service
 * Gera identificador único do dispositivo baseado em características do hardware/navegador
 * Similar ao sistema do mobpix2026
 */

class DeviceFingerprint {
  private cachedFingerprint: string | null = null;

  /**
   * Gera o fingerprint completo do dispositivo
   * Retorna uma string única que identifica o dispositivo
   */
  async generate(): Promise<string> {
    // Se já temos em cache, retorna
    if (this.cachedFingerprint) {
      return this.cachedFingerprint;
    }

    try {
      // Coletar todas as características do dispositivo
      const components = await this.collectComponents();
      
      // Criar string única combinando todas as características
      const fingerprintString = JSON.stringify(components);
      
      // Gerar hash SHA-256 para criar identificador único
      const fingerprint = await this.hashString(fingerprintString);
      
      // Salvar em cache
      this.cachedFingerprint = fingerprint;
      
      console.log('[DeviceFingerprint] ✅ Fingerprint gerado:', fingerprint.substring(0, 16) + '...');
      
      return fingerprint;
      
    } catch (error) {
      console.error('[DeviceFingerprint] ❌ Erro ao gerar fingerprint:', error);
      
      // Fallback: usar fingerprint simplificado baseado em timestamp + localStorage
      return await this.generateFallbackFingerprint();
    }
  }

  /**
   * Coleta todas as características do dispositivo
   */
  private async collectComponents(): Promise<Record<string, any>> {
    const components: Record<string, any> = {};

    // Informações do navegador
    components.userAgent = navigator.userAgent || '';
    components.language = navigator.language || '';
    components.languages = navigator.languages?.join(',') || '';
    components.platform = navigator.platform || '';
    components.hardwareConcurrency = navigator.hardwareConcurrency || 0;
    components.deviceMemory = (navigator as any).deviceMemory || 0;
    components.maxTouchPoints = navigator.maxTouchPoints || 0;

    // Informações da tela
    components.screenWidth = screen.width || 0;
    components.screenHeight = screen.height || 0;
    components.colorDepth = screen.colorDepth || 0;
    components.pixelDepth = screen.pixelDepth || 0;
    components.availWidth = screen.availWidth || 0;
    components.availHeight = screen.availHeight || 0;

    // Informações de timezone
    components.timezone = Intl.DateTimeFormat().resolvedOptions().timeZone || '';
    components.timezoneOffset = new Date().getTimezoneOffset();

    // Canvas fingerprint (mais difícil de falsificar)
    try {
      const canvas = document.createElement('canvas');
      const ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.textBaseline = 'top';
        ctx.font = '14px Arial';
        ctx.fillText('DeviceFingerprint', 2, 2);
        components.canvas = canvas.toDataURL();
      }
    } catch (e) {
      components.canvas = 'error';
    }

    // WebGL fingerprint
    try {
      const canvas = document.createElement('canvas');
      const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
      if (gl) {
        const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
        if (debugInfo) {
          components.webglVendor = gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL);
          components.webglRenderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL);
        }
      }
    } catch (e) {
      components.webgl = 'error';
    }

    // Audio context fingerprint
    try {
      const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
      const oscillator = audioContext.createOscillator();
      const analyser = audioContext.createAnalyser();
      const gainNode = audioContext.createGain();
      const scriptProcessor = audioContext.createScriptProcessor(4096, 1, 1);

      gainNode.gain.value = 0;
      oscillator.connect(analyser);
      analyser.connect(scriptProcessor);
      scriptProcessor.connect(gainNode);
      gainNode.connect(audioContext.destination);

      oscillator.start(0);
      scriptProcessor.onaudioprocess = (e) => {
        const output = e.inputBuffer.getChannelData(0);
        const hash = this.simpleHash(output.slice(0, 100).join(''));
        components.audioFingerprint = hash;
        oscillator.stop();
        audioContext.close();
      };
    } catch (e) {
      components.audioFingerprint = 'error';
    }

    // Verificar se já existe device_id salvo (para preservar identidade)
    const existingDeviceId = localStorage.getItem('device_id');
    if (existingDeviceId) {
      components.existingDeviceId = existingDeviceId;
    }

    return components;
  }

  /**
   * Gera hash SHA-256 de uma string
   */
  private async hashString(str: string): Promise<string> {
    // Verificar se Web Crypto API está disponível
    if (typeof crypto !== 'undefined' && crypto.subtle) {
      const encoder = new TextEncoder();
      const data = encoder.encode(str);
      const hashBuffer = await crypto.subtle.digest('SHA-256', data);
      const hashArray = Array.from(new Uint8Array(hashBuffer));
      return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    }

    // Fallback: hash simples
    return this.simpleHash(str);
  }

  /**
   * Hash simples para fallback
   */
  private simpleHash(str: string): string {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash; // Convert to 32bit integer
    }
    return Math.abs(hash).toString(16);
  }

  /**
   * Gera fingerprint de fallback caso haja erro
   */
  private async generateFallbackFingerprint(): Promise<string> {
    // Tentar recuperar device_id existente
    let deviceId = localStorage.getItem('device_id');
    
    if (!deviceId) {
      // Gerar novo baseado em características básicas + timestamp
      const basicInfo = [
        navigator.userAgent || '',
        screen.width + 'x' + screen.height,
        navigator.language || '',
        new Date().getTimezoneOffset().toString()
      ].join('|');
      
      deviceId = 'fallback_' + await this.hashString(basicInfo + Date.now().toString());
      localStorage.setItem('device_id', deviceId);
    }
    
    return deviceId;
  }

  /**
   * Limpa o cache (útil para testes)
   */
  clearCache(): void {
    this.cachedFingerprint = null;
  }
}

// Exportar instância singleton
export const deviceFingerprint = new DeviceFingerprint();

// Exportar também como função global para compatibilidade
if (typeof window !== 'undefined') {
  (window as any).deviceFingerprint = {
    generate: () => deviceFingerprint.generate()
  };
}

