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
    public ClassList givenClass;
    public StockCosmetic stock;
}