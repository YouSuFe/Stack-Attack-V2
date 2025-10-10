public enum WeaponType
{
    Basic = 0,
    Missile = 1,
    Kunai = 2,
}

public enum WeaponPatternType
{
    StraightLine,          // basic horizontal row (with overflow rules)
    AlternatingBurst,      // L-R-L-R missiles
    FanSequential          // kunai: center, +step, -step, ...
}

public enum ProjectileMotionType
{
    Linear,
    Sine
}

public enum WeaponUpgradeType
{
    FireRate,              // +% to fire rate (per second)
    ProjectileAmount,      // +N projectiles per trigger
    Piercing               // +N pierces
}

public enum OverflowResolution
{
    Rows,                  // prefer adding rows when amount > limit
    RapidStreak            // prefer very fast overflow shots
}

public enum WeaponUpgradeScope
{
    AllWeapons,
    SpecificWeapon
}