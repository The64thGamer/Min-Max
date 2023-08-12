using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Achievements : MonoBehaviour
{
    [SerializeField]Dictionary<string, float> achievements;

    public void AddToValue(string key, float value)
    {
        float val;
        if (achievements.TryGetValue(key, out val))
        {
            achievements[key] = val + value;
        }
        else
        {
            achievements.Add(key, value);
        }
    }

    public void SaveAchievements()
    {
        for (int i = 0; i < achievements.Count; i++)
        {
            PlayerPrefs.SetFloat(achievements.ElementAt(i).Key, PlayerPrefs.GetInt(achievements.ElementAt(i).Key) + achievements.ElementAt(i).Value);
        }
    }
}
