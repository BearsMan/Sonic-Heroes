using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fly type characters: Tails (Team Sonic), Rouge (Team Dark), Cream (Team Rose), Charmy (Team Chaotix)
public abstract class FlyCharacter : BaseCharacter
{
    // -------------------------------------------------------------------------
    // FLY CHARACTER MOVE NOTES (Sonic Heroes - US)
    // -------------------------------------------------------------------------

    // --- FORMATION PASSIVE: AUTO HOMING (triggered by Power type in Power Formation) ---
    // When the Power type character approaches enemies in Power Formation, the Fly type
    // member automatically jumps up and attacks with the Homing Attack alongside the
    // Speed type member.

    // --- ASCENDING FLIGHT (Hold Jump Button in Air) ---
    // The Fly type member starts flying into the air (Tails uses his twin tails, Rouge
    // and Charmy use their wings, Cream uses her ears). The Speed type member grabs onto
    // the Fly type member's feet, and the Power type member grabs onto the Speed type
    // member's feet, so the entire team is carried.
    // In gameplay: allows movement through the air in any direction, reaching elevated
    // areas, targeting far-away airborne objects, and traversing large gaps and bottomless
    // pits. There is a maximum height the team can reach. Duration is controlled by the
    // Flight Gauge, which depletes only while the player is moving. When the gauge empties,
    // the characters automatically drop from their current position.
    // To perform: Team must be in Fly Formation. Jump into the air, then press and hold
    // the Jump button (PSXButton / XboxA / A-GCN). Steer with the left control stick.

    // --- QUICK ASCENT (Jump Button pressed during Ascending Flight) ---
    // The Fly type member performs a sudden upward dash while the rest of the team hangs
    // on. The Fly type member is surrounded by their respective aura during the move.
    // In gameplay: allows the player to reach greater heights faster while in Ascending
    // Flight, though the maximum flight height still applies. Also doubles as a quick
    // attack; enemies contacted during Quick Ascent are stunned, and airborne enemies
    // are dropped to the ground (similar to Thunder Shoot).
    // To perform: Press the Jump button (PSXButton / XboxA / A-GCN) while using
    // Ascending Flight.

    // --- SPIN JUMP ---
    // Each playable character is surrounded by a sphere of their respective aura. Deals
    // one hit of damage to weaker or unprotected enemies. Using it on enemies allows the
    // character to bounce continuously on them and deal more damage until the enemy
    // retaliates. The super transformation does not enhance the Spin Jump's size. When
    // attacking Metal Madness with the Homing Attack, the player will bounce back in
    // the Spin Jump formation.
    // To perform: Press Jump button (PSXButton / XboxA / A-GCN).
    // Used by: All Fly type characters.

    // -------------------------------------------------------------------------
    // ACTION BUTTON (SOLO) ATTACKS
    // -------------------------------------------------------------------------

    // Dummy Ring Bomb (Tails - Team Sonic | Rouge - Team Dark):
    //   The user throws three Dummy Rings in front of them. When enemies contact the
    //   Dummy Rings, the Rings explode in an electrical field that paralyzes and damages
    //   the enemy.
    //   To perform: Team (Team Sonic or Team Dark) must be in Fly Formation. Press Action
    //   button (PSSquareButton / XboxX / GCN-B) without any teammates nearby.

    // Cheese Attack (Cream - Team Rose):
    //   Cream sends her Chao, Cheese, charging into enemies, destroying them on contact.
    //   This is the slowest Solo Attack of all Fly type characters, but unlike the others
    //   it destroys rather than merely stunning enemies. Very effective during battles
    //   and boss fights, capable of knocking opposing teams off platforms.
    //   To perform: Press Action button (PSSquareButton / XboxX / GCN-B) when Cream is
    //   without teammates.
    //   Note: This also serves as the Fly Formation and Solo Attack for Team Rose.

    // Sting Attack (Charmy - Team Chaotix):
    //   Charmy points his natural stinger forward and delivers a quick, vicious sting to
    //   opponents directly ahead. Charmy is surrounded by a red aura while performing
    //   this move.
    //   The Sting Attack also allows the player to activate Warp Flowers.
    //   To perform: Team Chaotix must be in Fly Formation. Press Action button
    //   (PSSquareButton / XboxX / GCN-B) without any teammates nearby.

    // -------------------------------------------------------------------------
    // TEAM ACTION BUTTON ATTACK
    // -------------------------------------------------------------------------

    // Thunder Shoot (All Fly type characters, requires at least one teammate):
    //   The Fly type member punts one of the team members currently holding on to them
    //   forward, like kicking a ball. The launched team member is enveloped in a crackling
    //   shield of electricity. Upon hitting an enemy, the enemy is temporarily stunned and
    //   takes damage if the Thunder Shoot is powerful enough. The launched teammate
    //   automatically returns to their position after impact. Armored enemies are immune
    //   to damage from this move but can still be stunned.
    //   Aside from attacking enemies, Thunder Shoot can be used to:
    //     - Trigger Target Switches in the air.
    //     - Shoot airborne Cages down from the air.
    //     - Partially fill the Team Blast Gauge.
    //   When used on airborne enemies, the move causes them to drop to the ground.
    //   Thunder Shoot can be used in succession as long as teammates are present.
    //
    //   Power levels (upgraded by collecting yellow Power Cores):
    //     Level 0: Stuns enemies only; target recovers after a short time. Hits one target.
    //              The launched team member receives damage when the attack connects.
    //     Level 1: Deals one hit point of damage and stuns target for a slightly longer
    //              time. Can possibly hit two targets per shot.
    //     Level 2: Slight chance of destroying the target outright. Stun effect lasts a
    //              long time. Can hit multiple targets.
    //     Level 3: Inflicts heavy damage and stun. Hits all targets within shooting range
    //              and draws in nearby Rings.
    //
    //   To perform: Team must be in Fly Formation with at least one other team member
    //   present (or the move will fail). Press Action button (PSSquareButton / XboxX /
    //   GCN-B) to launch teammates. Launched teammate homes in on any nearby target.

    // -------------------------------------------------------------------------

    public override void PowerAttack()
    {
        // Quick Ascent
        // The Fly type member performs a sudden upward dash during Ascending Flight,
        // surrounding themselves in their respective aura. Gets the team to greater heights
        // faster and stuns any enemies contacted during the dash (airborne enemies drop to
        // the ground).
        // To perform: Press Jump button (PSXButton / XboxA / A-GCN) while using
        // Ascending Flight.
    }

    public override void RegularAttack()
    {
        // Dispatches the appropriate attack based on which Fly type character is active
        // and the team context.

        // --- SOLO (no teammates nearby) ---
        // Dummy Ring Bomb - Tails (Team Sonic) and Rouge (Team Dark)
        // Cheese Attack   - Cream (Team Rose)
        // Sting Attack    - Charmy (Team Chaotix); also activates Warp Flowers

        // --- WITH TEAMMATES (at least one teammate present, Fly Formation) ---
        // Thunder Shoot   - All Fly type characters
        //   Level 0: Stun only, one target, launched member takes damage on connection
        //   Level 1: 1 HP damage, slightly longer stun, possibly two targets
        //   Level 2: Chance to destroy target, long stun, multiple targets
        //   Level 3: Heavy damage + stun, all targets in range, draws in nearby Rings

        // --- JUMP BUTTON ---
        // Spin Jump        - All Fly type characters
        // Ascending Flight - All Fly type characters (hold Jump in air, steered by stick)
        // Quick Ascent     - All Fly type characters (press Jump during Ascending Flight)
    }
}
