﻿using NetEscapades.EnumGenerators;

namespace GrowableGiantCrops;
public interface IGrowableGiantCropsAPI
{
}

[EnumExtensions]
public enum ResourceClumpIndexes
{
    Stump = 600,
    HollowLog = 602,
    Meteorite = 622,
    Boulder = 672,
    MineRockOne = 752,
    MineRockTwo = 754,
    MineRockThree = 756,
    MineRockFour = 758,

    Invalid = -999,
}