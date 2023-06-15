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

    public ClassStats GetClassStats(ClassList currentClass)
    {
        switch (currentClass)
        {
            case ClassList.labourer:
                return labourer;
            case ClassList.woodworker:
                return woodworker;
            case ClassList.engineer:
                return engineer;
            case ClassList.programmer:
                return programmer;
            case ClassList.computer:
                return computer;
            case ClassList.fabricator:
                return fabricator;
            case ClassList.artist:
                return artist;
            case ClassList.freelancer:
                return freelancer;
            case ClassList.craftsman:
                return craftsman;
            case ClassList.manager:
                return manager;
            default:
                return new ClassStats();
         }
    }
}

[SerializeField]
public enum ClassList
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

[SerializeField]
public enum TeamList
{
    orange,
    yellow,
    green,
    lightBlue,
    blue,
    purple,
    beige,
    brown,
    gray,
}

[System.Serializable]
public struct ClassStats
{
    public int baseHealth;
    public int baseSpeed;
    public int baseAccel;
    public float classEyeHeight;
}

[System.Serializable]
public struct MMChip
{
    public List<MMChipBuffNerfPair> buffsNerfs;
    public bool oneTimeUse;
}

[System.Serializable]
public struct MMChipBuffNerfPair
{
    public ChangableWeaponStats buffNerf;
    public MMChipApplier applier;
    public float statFloat;
    public int statInt;
    public bool statBool;
}

[System.Serializable]
public struct GunProjectiles
{
    public string gunName;
    public GameObject gunPrefab;
    public GameObject firePrefab;
    public GameObject altFirePrefab;
    public Vector3 firepoint;
}

public enum MMChipApplier
{
    add,
    multiply,
    set,
}
