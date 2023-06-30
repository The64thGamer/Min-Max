using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cosmetics : MonoBehaviour
{
    [SerializeField]
    List<Cosmetic>
        labourer,
        woodworker,
        developer,
        programmer,
        computer,
        fabricator,
        artist,
        freelancer,
        craftsman,
        manager;

    public List<Cosmetic> GetClassCosmetics(ClassList classes)
    {
        switch (classes)
        {
            case ClassList.labourer:
                return labourer;
            case ClassList.woodworker:
                return woodworker;
            case ClassList.developer:
                return developer;
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
                return null;
        }
    }
}

[Flags]
public enum BodyGroups
{
    head = 1,
    body = 2,
    armR = 4,
    armL = 8,
    handR = 16,
    handL = 32,
    legR = 64,
    legL = 128,
    footR = 256,
    footL = 512,
}

public enum EquipRegion
{
    hatHair,
    glasses,
    beard,
    neckwear,
    shirt,
    backpack,
    pants,
    belt,
    shoes,
    hands,
    shoulders,
}

public enum StockCosmetic
{
    none,
    stock,
    stockOptional,
}

[System.Serializable]
public struct Cosmetic
{
    public string name;
    public Texture2D icon;
    public GameObject prefab;
    public EquipRegion region;
    public BodyGroups hideBodyGroups;
    public StockCosmetic stock;
}