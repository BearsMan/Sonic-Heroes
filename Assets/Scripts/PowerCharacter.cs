// Power type characters: Knuckles (Team Sonic), E-123 Omega (Team Dark), Big the Cat (Team Rose), Vector (Team Chaotix)
public abstract class PowerCharacter : BaseCharacter
{
    // -------------------------------------------------------------------------
    // POWER CHARACTER MOVE NOTES (Sonic Heroes - US)
    // -------------------------------------------------------------------------

    // --- FORMATION PASSIVE: AUTO HOMING ---
    // When the Power type character approaches enemies in Power Formation with at least
    // one other teammate present, the Speed type and Fly type members automatically jump
    // up and attack enemies with the Homing Attack. They will also attack while the Power
    // type character is in motion. The power and effects of the Speed type member's Homing
    // Attack during Auto Homing match that member's current level (e.g. if the Speed type
    // has three blue Power Cores, the Homing Attack has Level 3 effects).
    // Note: At least one teammate must be present or the technique will fail.

    // --- GLIDE / DESCEND ABILITY (Hold Jump Button in Air) ---

    // Triangle Dive (Knuckles - Team Sonic | E-123 Omega - Team Dark):
    //   All three team members arrange themselves in midair into a triangle shape and put
    //   their hands together in the center, catching air beneath their bodies and greatly
    //   slowing their descent. Each member gives off their respective aura while descending.
    //   In gameplay: the player can slowly move in all directions while descending, similar
    //   to Glide. In areas with vertical drafts (e.g. fans), the player can catch the
    //   updraft and ascend to higher sections by using Triangle Dive above the draft source.
    //   To perform: Team must be in Power Formation with the Power type member as leader.
    //   Press and hold Jump button (PSXButton / XboxA / A-GCN) while jumping or in midair.
    //   Use the control stick to move in different directions. Release Jump to break.

    // Umbrella Descent (Big the Cat - Team Rose):
    //   Big pulls out his Fishing Rod in midair and unfolds its umbrella runner, catching
    //   the air beneath him and slowing his descent greatly. Amy and Cream grab onto Big's
    //   belt to ride with him. Big gives off a purple aura while descending.
    //   In gameplay: the player can slowly move in all directions while descending, similar
    //   to Glide. In areas with vertical drafts (e.g. fans), the player can catch the
    //   updraft and ascend to higher sections by using Umbrella Descent above the draft
    //   source.
    //   To perform: Team Rose must be in Power Formation with Big as leader. Press and
    //   hold Jump button (PSXButton / XboxA / A-GCN) while jumping or in midair. Use
    //   the control stick to move in different directions. Release Jump to break.
    //   Note: Umbrella Descent can be interrupted by taking damage or by performing Body
    //   Press during its execution.

    // Bubblegum Descent (Vector - Team Chaotix):
    //   Vector inflates a massive purple bubblegum balloon in midair, slowing his descent
    //   by catching the air beneath him. Using this ability near wind currents or fans
    //   allows Vector to accelerate in the direction of the winds. Vector gives off a green
    //   aura while descending. Vector can also damage unprotected Badniks by knocking into
    //   them while descending.
    //   To perform: Team Chaotix must be in Power Formation with Vector as leader. Press
    //   and hold Jump button (PSXButton / XboxA / A-GCN) while jumping or in midair.

    // -------------------------------------------------------------------------
    // ATTACK SEQUENCE: FORWARD -> REMOTE -> WIDE
    // The Power type character's attack sequence chains three moves in order.
    // Press Action button for Forward Power Attack, then again for Remote Power Attack,
    // then again for Wide Power Attack.
    // -------------------------------------------------------------------------

    // --- FORWARD POWER ATTACK (First press of Action button) ---
    // Each team's Forward Power Attack is unique:
    //   Team Sonic (Dash Punch): Knuckles grabs Tails (curled in spinball form) in his
    //     right hand and throws a powerful forward punch with his right fist, using Tails
    //     as a melee weapon.
    //   Team Dark (Dash Punch): Omega grabs Rouge (curled in spinball form) and retracts
    //     his right hand so Rouge rests in the wrist socket. He then throws a forward
    //     vertical punch with his right arm, using Rouge as a melee weapon.
    //   Team Rose (Umbrella Attack): Big pulls out his Fishing Rod with his left hand and
    //     uses it as a club to knock targets.
    //   Team Chaotix (Jaw Crush): Vector thrusts his head forward and crushes targets
    //     with his enormous jaws.
    // To perform: Team must be in Power Formation with the Power type member as leader.
    //   Press Action button (PSSquareButton / XboxX / GCN-B).

    // --- REMOTE POWER ATTACK (Second press of Action button, after Forward Power Attack) ---
    // The Power type character attacks surrounding enemies with a wide-ranged attack.
    // Each team's Remote Power Attack is unique:
    //   Team Sonic (Spinning Back Punch): While holding Sonic (curled in spinball form) in
    //     his right hand, Knuckles punches the ground with his right fist, creating a burst
    //     of fire on impact.
    //   Team Dark (Spinning Back Punch): While holding Shadow in his left wrist socket,
    //     Omega throws a vertical punch with his left arm, releasing a burst of fire in
    //     his wake.
    //   Team Rose (Fire Knock): Big, having entered Fire Combination, has one of his
    //     teammates Spin Jump off his shoulder. Big then uses his Fishing Rod to knock the
    //     teammate baseball-style at targets with such force that she turns into a
    //     destructive fireball, capable of damaging enemies, breaking down doors, and
    //     destroying other obstacles on the ground and in midair from afar. Can be used
    //     up to two times in a row depending on the number of teammates Big carries. When
    //     used near targets, the thrown teammates automatically home in on them.
    //   Team Chaotix (Fireball): Vector grabs his spinning teammates in his jaws and
    //     spits them out as destructive balls of fire.
    // To perform: Team must be in Power Formation with the Power type member as leader and
    //   must have performed the Forward Power Attack first. Press Action button
    //   (PSSquareButton / XboxX / GCN-B) immediately after.

    // --- WIDE POWER ATTACK (Third press of Action button, after Remote Power Attack) ---
    // The Power type character breaks out a final area-of-effect move in all directions,
    // damaging everything caught within it. Each team's Wide Power Attack is unique:
    //   Team Sonic (Volcanic Dunk): Knuckles releases his teammates and jumps slightly
    //     into the air, then punches the ground with such force it creates powerful
    //     volcanic explosions. Power levels (upgraded by red Power Cores):
    //       Level 1: Punches ground, creates a slight tremor and releases a flaming
    //                shockwave.
    //       Level 2: Bigger flaming shockwave and bigger tremor. Radius and damage doubled.
    //       Level 3: Creates a powerful volcanic eruption, sending fireballs spewing from
    //                the ground high into the air. Explosions can defeat almost any enemy.
    //                Maximum radius and damage.
    //     Special: During the Metal Overlord battle, Super Knuckles punches continuously
    //     into the air, releasing a fury of fireballs that explode on contact. Can be
    //     sustained by repeatedly pressing the Action button.
    //   Team Dark (Omega Arm): Omega releases his teammates and spins his torso clockwise
    //     while unleashing a full-force weapon barrage from his arm cannons in a spiral-
    //     like wave. Power levels (upgraded by red Power Cores):
    //       Level 1: Reconfigures claws into rotary cannons; fires bullets in a circular
    //                radius.
    //       Level 2: Reconfigures claws into Flamethrowers; sprays flames in a circular
    //                radius. Radius increased by a couple of meters; damage doubled.
    //                Note: missiles fire at a slightly upward angle and may not hit ground
    //                enemies from far distances.
    //       Level 3: Reconfigures claws into Missile Launchers; unleashes a barrage of
    //                drill-like missiles in a wide arc that create explosions on contact.
    //                Maximum radius and damage.
    //   Team Rose (Big Fishing): Big raises his Fishing Rod and reels out his lure, swinging
    //     it in a circular motion, damaging anything in its radius. Power levels (upgraded
    //     by red Power Cores):
    //       Level 1: Swings a burning, oversized red lure in a circle.
    //       Level 2: Swings a burning orange-and-white-striped lifebuoy with anchor symbols.
    //                Radius slightly increased; damage doubled.
    //       Level 3: Swings a large, burning metal spiked mace. Maximum radius and damage.
    //     To perform Big Fishing: Team Rose must be in Power Formation and must have
    //     performed the Fire Knock. Press Action button (PSSquareButton / XboxX / GCN-B)
    //     immediately after. The player can jump to prematurely end Big Fishing.
    //   Team Chaotix (Vector Breath): Vector falls down onto his tail, balancing slightly
    //     above the ground. He then faces forward and does a 360-degree spin on his tail
    //     while using a powerful mouth breath that damages anything in its radius.
    // To perform: Team must be in Power Formation and must have performed the Remote Power
    //   Attack first. Press Action button (PSSquareButton / XboxX / GCN-B) immediately after.

    // -------------------------------------------------------------------------
    // ADDITIONAL POWER-TYPE-SPECIFIC MOVES
    // -------------------------------------------------------------------------

    // Spin Jump: Each playable character is surrounded by a sphere of their respective
    //   aura. Deals one hit of damage to weaker or unprotected enemies. Using it on
    //   enemies allows the character to bounce continuously on them and deal more damage
    //   until the enemy retaliates. The super transformation does not enhance the Spin
    //   Jump's size.
    //   To perform: Press Jump button (PSXButton / XboxA / A-GCN).
    //   Used by: All Power type characters.

    // Fireball Jump (Fire Combination required, All Power type characters):
    //   The Power type character, while using Fire Combination, performs a Spin Jump into
    //   the air and releases the Speed type and Fly type members. While still in ball form,
    //   both released teammates revolve around the Power type character at high speed in a
    //   wide circle until the leader lands. Any targets caught within the range of the
    //   rotating teammates take damage.
    //   To perform: Team must be in Power Formation with the Power type as leader and must
    //   be performing Fire Combination. Press and hold Jump button (PSXButton / XboxA /
    //   A-GCN) to execute. Can also be executed by pressing Action button
    //   (PSSquareButton / XboxX / GCN-B) in Power Formation while airborne.

    // Hammer Down (Vector - Team Chaotix):
    //   Vector jumps into the air, puts his hands together into a collective fist above
    //   his head, and as he falls, thrusts his collective fist downward onto the opponent
    //   beneath him with his full playerRigidbody weight. The strike has enough force to smash through
    //   metal and create fiery shockwaves on impact with the ground. Vector gives off a
    //   green aura while falling.

    // Body Press (Big the Cat - Team Rose):
    //   Big jumps into the air while spreading his arms and legs and pointing his belly
    //   straight down, then slams his entire playerRigidbody weight down with enough force to create
    //   fiery shockwaves on the ground. Big gives off a purple aura while falling.
    //   After hitting a surface, Big bounces slightly back into the air. The Body Press
    //   can be used repeatedly by pressing the Action button again after bouncing or
    //   after damaging but not destroying an opponent. Can be interrupted by taking damage
    //   or by using Umbrella Descent during its execution.
    //   To perform: Team Rose must be in Power Formation. Press Action button
    //   (PSSquareButton / XboxX / GCN-B) during the jump.

    // Jump Fire Knock (Big the Cat - Team Rose, Fire Combination required):
    //   Big enters Fire Combination and jumps into the air, where either Amy or Cream
    //   Spin Jumps off his shoulders. Big then uses his Fishing Rod to knock the teammate
    //   baseball-style diagonally toward the ground with such force that the teammate
    //   turns into a fireball, creating an explosion on impact that damages everything in
    //   the vicinity. Allows the player to damage enemies, break down doors, and destroy
    //   obstacles from above.
    //   Can be used up to two times in a row depending on the number of teammates Big
    //   carries. When used near targets, the thrown teammates automatically home in on them.
    //   To perform: Team Rose must be in Power Formation with Big as leader. Enter Fire
    //   Combination and jump into the air, then press Action button
    //   (PSSquareButton / XboxX / GCN-B) to fire teammates.

    // Jump Fireball (Vector - Team Chaotix, Fire Combination required):
    //   Vector, having entered Fire Combination and holding Espio and Charmy as spinning
    //   balls in his jaws, jumps into the air. He then bends his head back and spits one
    //   of his teammates diagonally toward the ground with such force that the teammate
    //   turns into a fireball, creating an explosion on impact that damages everything in
    //   the vicinity. Allows the player to damage enemies, break open doors, and destroy
    //   obstacles from above.

    // -------------------------------------------------------------------------

    public override void PowerAttack()
    {
        // Wide Power Attack (third in attack chain, follows Remote Power Attack)
        // Team Sonic  -> Volcanic Dunk (Knuckles punches ground; volcanic eruption at Level 3)
        // Team Dark   -> Omega Arm (Omega spins torso; weapon barrage in circular radius)
        // Team Rose   -> Big Fishing (Big swings fishing rod lure in circle; spiked mace at Level 3)
        // Team Chaotix-> Vector Breath (Vector spins on tail; 360-degree mouth breath attack)
        // All upgradeable with red Power Cores (Levels 1-3).
        // To perform: Must have completed Remote Power Attack first, then press Action button.
    }

    public override void RegularAttack()
    {
        // Dispatches the appropriate attack based on context and attack chain position.

        // --- ATTACK CHAIN (ground, Power Formation) ---
        // 1st press: Forward Power Attack
        //   Team Sonic   -> Dash Punch (Knuckles punches forward using Tails as weapon)
        //   Team Dark    -> Dash Punch (Omega punches forward using Rouge as weapon)
        //   Team Rose    -> Umbrella Attack (Big swings Fishing Rod as a club)
        //   Team Chaotix -> Jaw Crush (Vector thrusts head forward and bites)
        //
        // 2nd press: Remote Power Attack (follows Forward Power Attack)
        //   Team Sonic   -> Spinning Back Punch (Knuckles punches ground with Sonic, fire burst)
        //   Team Dark    -> Spinning Back Punch (Omega punches with Shadow, fire burst)
        //   Team Rose    -> Fire Knock (Big knocks flaming teammate at targets; up to 2 uses)
        //   Team Chaotix -> Fireball (Vector spits flaming teammates at targets)
        //
        // 3rd press: Wide Power Attack -> see PowerAttack() above

        // --- AIRBORNE ---
        // Fireball Jump   - All Power type characters (Fire Combination active, hold Jump or Action in air)
        // Body Press       - Big the Cat (Team Rose) only (Action button during jump)
        // Hammer Down      - Vector (Team Chaotix) only (Action button during jump)
        // Jump Fire Knock  - Big the Cat (Team Rose) only (Fire Combination active, airborne)
        // Jump Fireball    - Vector (Team Chaotix) only (Fire Combination active, airborne)

        // --- JUMP BUTTON ---
        // Spin Jump        - All Power type characters
        // Triangle Dive    - Knuckles (Team Sonic) and E-123 Omega (Team Dark) (hold Jump in air)
        // Umbrella Descent - Big the Cat (Team Rose) only (hold Jump in air)
        // Bubblegum Descent- Vector (Team Chaotix) only (hold Jump in air)

        // --- FORMATION PASSIVE ---
        // Auto Homing      - All Power type characters (approach enemies; Speed + Fly attack automatically)
    }
}
