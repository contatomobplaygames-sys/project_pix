using UnityEngine;
using RotaryHeart.Lib.SerializableDictionary;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif
namespace Ads
{
    [System.Serializable]
    public class AdsObjectDicionary : SerializableDictionaryBase<string, AdsObject> { }

    [System.Serializable]
    public class AdsProbabilityDictionary : SerializableDictionaryBase<string, float> { }

    public class AdsSettings : ScriptableObject
    {
        public static AdsSettings Instance
        {
            get
            {
                if(_instance == null)
                {
#if UNITY_EDITOR
                    _instance = CreateOrFindAdsSettings();

#else
                    _instance = Resources.Load<AdsSettings>("AdsSettings");
#endif
                }

                return _instance;

            }
        }

        private static AdsSettings _instance;

        [Header("Interstitial Probability")]
        [Range(0f,1f)]
        public float defaultInterstitialProbability = 0.5f;
        public AdsProbabilityDictionary adsProbabilityDictionary = new AdsProbabilityDictionary();

        [Header("Advertisements")]
        [Space(10)]
        [SerializeField]
        private AdsObjectDicionary adsDictionary = new AdsObjectDicionary();

        public static AdsObject GetAdsObject(string adsKey)
        {
            try
            {
                return Instance.adsDictionary[adsKey];
            }
            catch
            {
                return GetAdsObject();
            }
        }

        public static AdsObject GetAdsObject()
        {
            return Instance.adsDictionary.Values.First();
        }

        /// <summary>
        /// Obtém a chave da rede de anúncios primária (primeira do dicionário)
        /// </summary>
        public static string GetPrimaryAdsKey()
        {
            try
            {
                if (Instance.adsDictionary == null || Instance.adsDictionary.Count == 0)
                    return null;
                
                // Obtém a primeira chave do dicionário
                var firstKey = Instance.adsDictionary.Keys.FirstOrDefault();
                return firstKey;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Define a chave primária de anúncios (disponível em runtime)
        /// </summary>
        public void SetPrimaryAdsKey(string primaryKey)
        {
            if (string.IsNullOrEmpty(primaryKey)) return;
            if (!adsDictionary.ContainsKey(primaryKey)) return;

            var newDict = new AdsObjectDicionary();
            // primary first
            newDict.Add(primaryKey, adsDictionary[primaryKey]);

            foreach (var kv in adsDictionary)
            {
                if (kv.Key == primaryKey) continue;
                if (!newDict.ContainsKey(kv.Key))
                    newDict.Add(kv.Key, kv.Value);
            }

            adsDictionary = newDict;
            Debug.Log("[AdsSettings] Primary ads key set to: " + primaryKey);
        }

#if UNITY_EDITOR
        [MenuItem("Smart Ads/Ads Settings")]
        private static void SetAssetPath()
        {
            Selection.activeObject = CreateOrFindAdsSettings();
        }

        public static AdsSettings CreateOrFindAdsSettings()
        {
            AdsSettings settings = GetScriptableAsset<AdsSettings>("AdsSettings", "AdsSettings");

            string path = "Assets/Ads/Resources/AdsSettings.asset";

            if (settings == null)
            {
                // Criar os diretórios necessários antes de criar o asset
                string directoryPath = Path.GetDirectoryName(path);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    AssetDatabase.Refresh();
                }

                settings = CreateInstance<AdsSettings>();
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = settings;
                Debug.Log("File GameSettings created at " + path + "!");
            }
            return settings;
        }

        private static T GetScriptableAsset<T>(string assetName, string assetType)
            where T : ScriptableObject
        {
            T asset = default;
            string[] gameSettingsGUIDs = AssetDatabase.FindAssets($"{assetName} t:{assetType}");
            if (gameSettingsGUIDs.Length > 0)
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(gameSettingsGUIDs[0]));
            return asset;
        }

#endif
    }
}
