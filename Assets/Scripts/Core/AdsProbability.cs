using System.Collections.Generic;
using UnityEngine;

namespace Ads
{
    public class AdsProbability
    {
        private readonly IDictionary<string, float> dictionaryProbability;
        private float defaultProbability;
        public AdsProbability(float defaultProbability)
        {
            this.dictionaryProbability = new Dictionary<string, float>();
            this.defaultProbability = defaultProbability;
        }
        public AdsProbability(IDictionary<string, float> probability, float defaultProbability)
        {
            this.dictionaryProbability = probability;
            this.defaultProbability = defaultProbability;
        }

        private float GetProbability(string probabilityKey)
        {
            if (dictionaryProbability.ContainsKey(probabilityKey))
                return dictionaryProbability[probabilityKey];
            else
                return GetProbability();
        }

        private float GetProbability()
        {
            return defaultProbability;
        }

        private float GetRandomNumber()
        {
            Random.InitState(System.DateTime.Now.Millisecond);

            return Random.Range(0f, 1f);
        }

        public bool IsShowAd(string probabilityKey) { 

            float prob = GetRandomNumber();

            return prob <= GetProbability(probabilityKey);
        }

        public bool IsShowAd()
        {
            float prob = GetRandomNumber();

            return prob <= GetProbability();
        }
    }
}
