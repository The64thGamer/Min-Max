using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class AllStats : MonoBehaviour
{
    [SerializeField] ClassStats 
        labourer,
        woodworker,
        engineer,
        programmer,
        computer,
        fabricator,
        artist,
        freelancer,
        craftsman,
        manager;
    [Header("MM-Chips")]
    [SerializeField] List<MMChip> mMChips;
    [Header("Guns")]
    [SerializeField] List<GunProjectiles> guns;

    public GunProjectiles SearchGuns(string gun)
    {
        for (int i = 0; i < guns.Count; i++)
        {
            if (guns[i].gunName == gun)
            {
                return guns[i];
            }
        }
        return new GunProjectiles();
    }
}

[SerializeField]
enum ClassList
{
    labourer,
    woodworker,
    engineer,
    programmer,
    computer,
    fabricator,
    artist,
    freelancer,
    craftsman,
    manager
}

[System.Serializable]
public struct ClassStats
{
    [SerializeField] int baseHealth;
    [SerializeField] int baseSpeed;
}

[System.Serializable]
public struct MMChip
{
    [SerializeField] List<MMChipBuffNerfPair> buffsNerfs;
}

[System.Serializable]
public struct MMChipBuffNerfPair
{
public ChangableWeaponStats buffNerf;
    [SerializeField] MMChipApplier applier;
    [SerializeField] float statFloat;
    [SerializeField] int statInt;
    [SerializeField] bool statBool;
}

[System.Serializable]
public struct GunProjectiles
{
    public string gunName;
    public GameObject gunPrefab;
    public GameObject firePrefab;
    public GameObject altFirePrefab;
}


[SerializeField]
enum MMChipApplier
{
    add,
    multiply,
    set,
}
