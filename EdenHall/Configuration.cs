using Dalamud.Configuration;
using Dalamud.Plugin;
using ECommons.Configuration;
using System;

namespace EdenHall;

public class Configuration : IEzConfig
{
    public int Accept_Trade_Delay = 1000;
    public int MinGil = 1;
    public int DealerStandThreshold { get; internal set; }
    public bool DealerHitOnSoft { get; internal set; }
}