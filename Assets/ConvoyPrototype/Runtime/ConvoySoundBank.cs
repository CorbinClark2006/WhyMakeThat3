using UnityEngine;

namespace Convoy
{
    public sealed class ConvoySoundBank : ScriptableObject
    {
        public AudioClip engine, wind, fire, impact, pickup, load;

        public void AssignTo(ConvoyPrototype game)
        {
            game.engineSound = engine;
            game.windSound = wind;
            game.fireSound = fire;
            game.impactSound = impact;
            game.pickupSound = pickup;
            game.loadSound = load;
        }
    }
}
