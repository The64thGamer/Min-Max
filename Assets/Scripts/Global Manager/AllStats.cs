using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class AllStats : MonoBehaviour
{
    [SerializeField]
    ClassStats 
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

    [SerializeField]
    List<MMChip> mMChips;
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
    [SerializeField] ChangableWeaponStats buffNerf;
    [SerializeField] MMChipApplier applier;
    [SerializeField] float statFloat;
    [SerializeField] int statInt;
    [SerializeField] bool statBool;
}

[SerializeField]
enum MMChipApplier
{
    add,
    multiply,
    set,
}
