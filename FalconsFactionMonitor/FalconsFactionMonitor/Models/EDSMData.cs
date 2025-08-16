using System;
using System.Collections.Generic;

namespace FalconsFactionMonitor.Models
{
    public class EDSMSystems
    {
        public int id { get; set; }
        public long id64 { get; set; }
        public string name { get; set; }
        public Coordinates coords { get; set; }
        public string allegiance { get; set; }
        public string government { get; set; }
        public string state { get; set; }
        public string economy { get; set; }
        public string security { get; set; }
        public int population { get; set; }
        public ControllingFaction controllingFaction { get; set; }
        public List<Factions> factions { get; set; }
        public List<Stations> stations { get; set; }
        public List<Bodies> bodies { get; set; }
        public string date { get; set; }
    }
    public class Coordinates
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }
        public class EDSMFactions
    {
        public int id { get; set; }
        public long id64 { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public ControllingFaction controllingFaction { get; set; }
        public List<Factions> factions { get; set; }
    }
    public class ControllingFaction
    {
        public int id { get; set; }
        public string name { get; set; }
        public string allegiance {  get; set; }
        public string government { get; set; }
    }
    public class Factions
    {
        public int id { get; set; }
        public string name { get; set; }
        public string allegiance { get; set; }
        public string government { get; set; }
        public decimal influence { get; set; }
        public string state { get; set; }
        public List<State> activeStates { get; set; }
        public List<State> recoveringStates { get; set; }
        public List<State> pendingStates { get; set; }
        public string happiness {  get; set; }
        public bool isPlayer { get; set; }
        public int lastUpdate { get; set; }
    }
    public class State
    {
        public string state { get; set; }
    }
    public class Stations
    {
        public int id { get; set; }
        public long marmarketId { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public double distanceToArrival { get; set; }
        public string allegiance { get; set; }
        public string government { get; set; }
        public string economy { get; set; }
        public string secondEconomy { get; set; }
        public bool haveMarket { get; set; }
        public bool haveShipyard { get; set; }
        public bool haveOutfitting { get; set; }
        public List<String> otherServices { get; set; }
        public ControllingFaction controllingFaction { get; set; }
        public UpdateTime updateTime { get; set; }
    }
    public class UpdateTime
    {
        public string information { get; set; }
        public string market { get; set; }
        public string shipyard { get; set; }
        public string outfitting { get; set; }
    }
    public class Bodies
    {
        public int id { get; set; }
        public long id64 { get; set; }
        public int bodyId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string subType { get; set; }
        public Parents parents { get; set; }
        public int distanceToArrival { get; set; }
        public bool isLandable { get; set; }
        public double gravity { get; set; }
        public double earthMasses { get; set; }
        public double radius { get; set; }
        public double surfaceTemperature { get; set; }
        public double surfacePressure { get; set; }
        public string volcanismType { get; set; }
        public string atmosphereType { get; set; }
        public string atmosphereComposition { get; set; }

    }
    public class Parents
    {
        public int Star { get; set; }
        public int Null { get; set; }
        public int Planet {  get; set; }
    }
}
