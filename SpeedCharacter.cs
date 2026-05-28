using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Speed type characters: Sonic (Team Sonic), Shadow (Team Dark), Amy Rose (Team Rose), Espio (Team Chaotix)
public abstract class SpeedCharacter : BaseCharacter
{
    // -------------------------------------------------------------------------
    // SPEED CHARACTER MOVE NOTES (Sonic Heroes - US)
    // -------------------------------------------------------------------------

    // --- FORMATION PASSIVE: AUTO HOMING (triggered by Power type in Power Formation) ---
    // When the Power type character approaches enemies in Power Formation, the Speed type
    // member automatically jumps up and attacks with the Homing Attack. The power and
    // effects of this Auto Homing Homing Attack match the Speed type member's current level.

    // --- GLIDE / DESCEND ABILITY (Hold Jump Button in Air) ---
    // Triangle Jump (Sonic, Shadow, Espio): Press Jump to jump onto a wall, then use
    //   the control stick / directional input to jump to a new section of the wall.
    //   Used by all Speed type characters.

    // --- REGULAR / ACTION BUTTON ATTACKS ---

    // Spin Jump: Each playable character is surrounded by a sphere of their respective
    //   aura. Deals one hit worth of damage to weaker or unprotected enemies. Using it on
    //   enemies allows the character to bounce continuously on them and deal more damage
    //   until the enemy retaliates. The super transformation does not enhance the Spin
    //   Jump's size. When attacking Metal Madness with the Homing Attack, the player will
    //   bounce back in the Spin Jump formation.
    //   To perform: Press Jump button (PSXButton / XboxA / A-GCN).
    //   Used by: All Speed type characters.

    // Homing Attack: Press Jump while airborne to lock onto and dash at the nearest enemy
    //   or item. Will also automatically target Springs and other interactive objects.
    //   To perform: Press Jump while in the air.
    //   Used by: Sonic (Team Sonic) as leader. All Speed characters can use it via Auto Homing.

    // Light Speed Dash: Hold the Action button near a trail of Rings to dash through them
    //   at light speed.
    //   To perform: Press and Hold Action button (PSSquareButton / XboxX / GCN-B) near a
    //   trail of Rings.
    //   Used by: Sonic (Team Sonic) only.

    // Light Speed Attack: A charged version of the Homing Attack. Hold the Action button
    //   to charge, then release to attack all enemies in range simultaneously at light speed.
    //   To perform: Hold then release Action button.
    //   Used by: Sonic (Team Sonic) only (requires charge-up, unlocked via leveling).

    // Tornado Jump / Pole Swing: Press Jump to jump, then press the Action button
    //   (PSSquareButton / XboxX / GCN-B) to grab and swing on a pole. Momentum carries
    //   the character to new platforms.
    //   To perform: Jump (A), then Action button while near a pole.
    //   Used by: Sonic (Team Sonic) only.

    // Propeller Hammer: Amy jumps up into the air and swings her Piko Piko Hammer above
    //   her head in a helicopter-like spinning motion, slowing her descent and damaging
    //   enemies caught above her.
    //   To perform: Press and Hold Jump button (PSXButton / XboxA / A-GCN) while in the air.
    //   Used by: Amy Rose (Team Rose) only. Replaces Triangle Jump for Amy.

    // Shuriken: Espio throws a ninja shuriken directly ahead, dealing damage to enemies
    //   from a distance.
    //   To perform: Press Action button (PSSquareButton / XboxX / GCN-B) while grounded
    //   without teammates nearby.
    //   Used by: Espio (Team Chaotix) only.

    // Swinging Hammer Attack: Amy charges her Piko Piko Hammer and releases it in a wide
    //   swing, dealing heavy damage to enemies directly in front of her.
    //   To perform: Team Rose must be in Speed Formation. Hold the Action button
    //   (PSSquareButton / XboxX / GCN-B), then release without any teammates nearby.
    //   Used by: Amy Rose (Team Rose leader) only.

    // --- JUMP + ACTION BUTTON ATTACKS (Mid-Air Action Button) ---

    // Blue Tornado: Sonic uses the Spin Dash to circle around an opponent in midair at
    //   high speed, leaving a blue trail. The resulting slipstream forms a blue cyclonic
    //   vortex around the opponent.
    //   Effects by enemy type:
    //     - Unprotected Egg Pawns / Egg Knights / opposing teams: sucked into the air,
    //       fall and take damage; may be stunned or have weak spots revealed.
    //     - Shielded Egg Pawns / Egg Knights: permanently blows off their shields.
    //     - Larger or airborne enemies: disorients but deals no damage.
    //   Also usable to scale Poles and activate propellers.
    //   To perform: Team Sonic must be in Speed Formation. Press Action button
    //   (PSSquareButton / XboxX / GCN-B) during a jump. Sonic auto-homes to nearest
    //   enemy in range, including enemies above him.
    //   Used by: Sonic (Team Sonic) only.

    // Black Tornado: Shadow uses the Spin Dash to circle around an opponent in midair at
    //   high speed, leaving a yellow trail. The resulting slipstream forms a black cyclonic
    //   vortex around the opponent.
    //   Effects by enemy type:
    //     - Unprotected Egg Pawns / Egg Knights / opposing teams: sucked into the air,
    //       fall and take damage; may be stunned or have weak spots revealed.
    //     - Shielded Egg Pawns / Egg Knights: permanently blows off their shields.
    //     - Larger or airborne enemies: disorients but deals no damage.
    //   Also usable to scale Poles and activate propellers.
    //   To perform: Team Dark must be in Speed Formation. Press Action button
    //   (PSSquareButton / XboxX / GCN-B) during a jump. Shadow auto-homes to nearest
    //   enemy within a reasonable distance, including enemies above him.
    //   Used by: Shadow (Team Dark) only.

    // Leaf Swirl: Espio jumps into the air, does a body flip with hands folded, then
    //   creates a cyclonic vortex of transparent leaves. Simultaneously invokes his
    //   camouflage ability, turning him invisible.
    //   Effects by enemy type:
    //     - Unprotected Egg Pawns / Egg Knights / opposing teams: sucked into the air,
    //       fall and take damage; may be stunned or have weak spots revealed.
    //     - Shielded Egg Pawns / Egg Knights: permanently blows off their shields.
    //     - Larger or airborne enemies: disorients but deals no damage.
    //   While invisible: Espio cannot be detected by enemies or Giant Frogs, can pass
    //   through laser fields without damage, and retains his full moveset (except Rocket
    //   Accel). Vector and Charmy will not follow while Espio is invisible. The player can
    //   still take damage. Invisibility ends if the player takes damage, uses Leaf Swirl
    //   again, changes leader, or activates Chaotix Recital.
    //   Also usable to scale Poles and activate propellers.
    //   To perform: Team Chaotix must be in Speed Formation with Espio as leader. Press
    //   Action button (PSSquareButton / XboxX / GCN-B) during a jump. Espio must be near
    //   or above the target.
    //   Used by: Espio (Team Chaotix) only.

    // Tornado Hammer: Amy jumps into the air, pulls out her Piko Piko Hammer, and swings
    //   it around herself once, creating a cyclonic vortex of pink hearts launched at
    //   targets from afar. Amy gives off pink hearts while performing this move.
    //   Effects by enemy type:
    //     - Unprotected Egg Pawns / Egg Knights / opposing teams: sucked into the air,
    //       fall and take damage; may be stunned or have weak spots revealed.
    //     - Shielded Egg Pawns / Egg Knights: permanently blows off their shields.
    //     - Larger or airborne enemies: disorients but deals no damage.
    //   Also usable to scale Poles and activate propellers.
    //   To perform: Team Rose must be in Speed Formation. Press Action button
    //   (PSSquareButton / XboxX / GCN-B) during a jump. Amy must be facing the target.
    //   Used by: Amy Rose (Team Rose) only.

    // --- GROUND HOLD ATTACKS ---

    // Spin: The Speed type character curls into a spin and rolls forward, damaging enemies
    //   on contact.
    //   To perform: Team must be in Speed Formation with the Speed type as leader. Hold
    //   down the Action button (PSSquareButton / XboxX / GCN-B). Use movement controls
    //   to steer while the Spin is active.
    //   Used by: All Speed type characters.

    // -------------------------------------------------------------------------

    public override void PowerAttack()
    {
        // Rocket Accel
        // The Speed type character rockets forward at extreme speed in a straight line,
        // dealing heavy damage to any enemies in the path.
        // To perform: Hold Action button (PSSquareButton / XboxX / GCN-B) while grounded.
        // Used by: All Speed type characters EXCEPT Espio while invisible (Leaf Swirl active).
    }

    public override void RegularAttack()
    {
        // Dispatches the appropriate attack based on which Speed type character is active
        // and whether they are grounded or airborne.

        // --- GROUNDED ---
        // Spin            - All Speed type characters (hold Action button)
        // Shuriken        - Espio only (tap Action button, no teammates nearby)
        // Swinging Hammer - Amy Rose only (hold then release Action button, no teammates nearby)

        // --- AIRBORNE (Action button during jump) ---
        // Blue Tornado    - Sonic (Team Sonic) only
        // Black Tornado   - Shadow (Team Dark) only
        // Leaf Swirl      - Espio (Team Chaotix) only
        // Tornado Hammer  - Amy Rose (Team Rose) only

        // --- JUMP BUTTON (airborne) ---
        // Spin Jump       - All Speed type characters
        // Homing Attack   - All Speed type characters (auto-targets nearest enemy/item/spring)
        // Triangle Jump   - Sonic, Shadow, Espio (wall-jump; NOT available to Amy Rose)
        // Propeller Hammer- Amy Rose only (replaces Triangle Jump; hold Jump button in air)
        // Tornado Jump    - Sonic only (swing on poles; Jump then Action button near pole)
        // Light Speed Dash- Sonic only (hold Action button near Ring trail)
        // Light Speed Attack - Sonic only (hold then release Action button to charge)
    }
}
