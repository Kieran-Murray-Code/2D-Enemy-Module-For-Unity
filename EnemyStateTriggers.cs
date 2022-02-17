using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStateTriggers
{
}

public  enum EnemyStateTrigger
{
    PlayerEntersLineOfSight,
    PlayerExitsLineOfSight,
    PlayerEntersVicinity,
    PlayerExitsVicinity,
    IsHitByDisc,
    CollisionWithPlayer,
    CollisionWithWall,
    CollisionWithCeiling,
    ReachedLedge,
    ReachedTarget,
    TimeInStateEquals,
    HealthIsBelow,
    StateIsFinished,
    BecameGrounded,
    BecameAirBorne,
    HealthIsBelowInstant,
    YVelocityIsBelow,
    YVelocityIsAbove,
    XVelocityIsBelow,
    XVelocityIsAbove
}

