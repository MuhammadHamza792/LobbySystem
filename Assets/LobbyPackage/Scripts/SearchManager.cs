using System.Collections.Generic;
using UnityEngine;

namespace LobbyPackage.Scripts
{
    public class SearchManager : Singleton<SearchManager>
    {
        public List<T> Search<T>(List<T> items, string search) where T : MonoBehaviour
        {
            var availableItems = new List<T>();
        
            foreach (var item in items)
            {
                if(string.Equals(item.name.Substring(0, item.name.Length), search))
                    availableItems.Add(item.gameObject.GetComponent<T>());
            }

            foreach (var availableItem in availableItems)
            {
                Debug.Log(availableItem.name);
            }
        
            return availableItems;
        }
    }
}
