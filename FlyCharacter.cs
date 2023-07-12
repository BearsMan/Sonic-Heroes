using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FlyCharacter : BaseCharacter
{
    //These comments are for Fly Character Attacks

    //When performing Ascending Flight, the Fly type member starts flying into the air (Tails uses his twin tails, Rouge and Charmy use their wings, and Cream uses her ears). At the same time, the Speed type member grabs onto the Fly type member's feet and the Power type member grabs onto the Speed type member's feet. As a result, the Fly type member flies through the air while the remainder of their team rides with them.

    //In gameplay, Ascending Flight allows the player to move through the air in any direction, reach otherwise unreachable elevated places, target far-away objects in mid-air, and traverse over large gaps and bottomless pits.However, the player can only reach a certain height with Ascending Flight before they stop ascending.Also, the duration of the move is determined by the Flight Gauge which depletes only when the player moves. When it empties, the playable characters will automatically drop down from where they would be in flight at this point.

    //To perform Ascending Flight in gameplay, the selected team must be in Fly Formation. The player then has to jump into the air and hold down PSXButton.png/XboxA.png/A Button GameCube v2.png to fly.The player can control the characters' movement with the left control stick.

    //the Chao Attack is called the Cheese Attack.In this game, it serves as the Fly Formation and Solo Attack for Team Rose. When using the Cheese Attack, Cream sends Cheese into enemies and destroys them on contact (unlike other Solo Attacks for the Fly Formation which only stuns enemies), though this attack is the slowest of them all.It is very useful during Battles and boss battles with other teams, capable of knocking the opposing team off the platform.

    //To use the Cheese Attack in gameplay, the player has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png when Cream is without her teammates.

    //The Dummy Ring Bomb first appeared in Sonic Heroes, where it is a Fly Formation maneuver and a Solo Attack used by Tails on Team Sonic and Rouge on Team Dark. In this game, when using the Dummy Ring Bomb, the user throws three Dummy Rings in front them. When enemies make contact with these Dummy Rings, the Rings explode in an electrical field that paralyzes and damages the enemy.

    //To use the Dummy Ring Bomb in gameplay, the selected team(either Team Sonic or Team Dark) must be in Fly Formation.The player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png without any teammates nearby to use it.

    //When performing the Spin Jump in this game, each playable character is surrounded by a sphere of their respective auras. In gameplay, the Spin Jump deals one hit worth of damage to weaker or unprotected enemies. Additionally, using it on enemies allows the character to bounce continuously on them and deal more damage until the enemy retaliates. Furthermore, depending on which character is the leader, the player can follow the Spin Jump up with a more useful maneuver or offensive ability. Again, the super transformation does not enhance the Spin Jump's size. It should be noted that when attacking Metal Madness with the Homing Attack, the player will bounce back in the Spin Jump formation.

    //To perform this move in gameplay, the player must press A Button GameCube v2.png/XboxA.png/PSXButton.png.

    //Note: only Charmy (Team Chaotix uses this) When using the Sting Attack, Charmy points his natural stinger out in front of him and delivers a quick, vicious sting to the opponents directly ahead. While doing so, Charmy is surrounded by a red aura.

    //To perform Sting Attack in gameplay, Team Chaotix must be in Fly Formation.The player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png without any teammates nearby to execute the Sting Attack. The Sting Attack also allows the player to activate Warp Flowers.

    //When performing Thunder Shoot, the Fly Type member on the team punts one of the team members currently holding onto them forward, like kicking a ball. In the process, the launched team member is conferred in a crackling shield of electricity. Once the launched team member hits an enemy, the enemy in question will become temporarily stunned and also take damage if the Thunder Shoot is powerful enough, while the launched teammate returns to their position in the team formation. Armored enemies are immune to damage from this move, however.

    //By collecting yellow Power Cores during gameplay, the player can increase the power, effect, and radius of the Thunder Shoot.The levels of power are as follows:

    //Level 0: Thunder Shoot will only stun enemies and the target will recover from the stun after a short time.It can only hit one target.Shot team member will receive damage when the attack connects.

    //Level 1: Thunder Shoot will damage the target by one hit point and stun the target for a slightly longer time. It can possibly hit two targets during one shot.

    //Level 2: Thunder Shoot has a slight chance of destroying the target. If not destroyed, the stun effect will last a long time. It can hit multiple targets.

    //Level 3: Thunder Shoot inflicts heavy damage on the target and inflicts stun.It can hit all targets within shooting range and draws in nearby Rings.

    //To perform Thunder Shoot in gameplay, the selected team must be in Fly Formation and have at least one other team member present or the move will fail. The player then has to press the Attack button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) to launch the teammates at enemies.When launched away, the teammate will home on to any nearby target.When used on airborne enemies, the move will cause them to drop to the ground. For as long as the player has other team members present, the player can continue to use Thunder Shoot in succession.

    //Aside from attacking enemies, the player can also use Thunder Shoot to trigger Target Switches in the air, shoot airborne Cages down from the air, or fill the Team Blast Gauge (although it fills a very partial amount).

    //When using the Quick Ascent, the Fly type member of the team performs a sudden dash upwards when using Ascending Flight while the rest of their team hangs on to them. While doing that, the Fly type member is surrounded by their respective aura.

    //In gameplay, the Quick Ascent allows the player to get higher into the air faster when using Ascending Flight, though the player cannot go any higher than the maximum flying height. Additionally, the Quick Ascent doubles as a quick attack on enemies; if coming into contact with enemies during the Quick Ascent, the enemies will be stunned, and hit airborne enemies will drop to the ground, similar to Thunder Shoot.

    //To perform Quick Ascent in gameplay, the player has to press PSXButton.png/XboxA.png/A Button GameCube v2.png while using Ascending Flight.
}
