using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpeedCharacter : BaseCharacter
{
    //These comments are for Speed Character Attacks

    //X spin dash

    //Hold B Rocket Accel

    //Press A While In Air To Perform Homing Attack It Will Also Automatically Target Other Items and Springs

    //Press and Hold B (Use Light Speed Dash near a trail of rings

    //Tornado Jump (Press A to jump then press B (PC) to swing on pole

    //Triangle Jump (Press A to Jump on to a wall then while on the wall use the control stick or select a key to jump to a new section of the wall

    //Propeller Hammer jump up to midair and swings hammer at 360 degrees Note: Only used by Amy Rose in Team Rose

    //Shuriken (move) Note: Only used by Espio (Team Chaotix)

    //Spin To perform the Spin in gameplay, the selected team must be in Speed Formation with the Speed Type member as the leader. Then player must then hold down While the Spin is in effect, the player can use the movement controls to steer the Spin.

    //Spin Jump: When performing the Spin Jump in this game, each playable character is surrounded by a sphere of their respective auras. In gameplay, the Spin Jump deals one hit worth of damage to weaker or unprotected enemies. Additionally, using it on enemies allows the character to bounce continuously on them and deal more damage until the enemy retaliates. Furthermore, depending on which character is the leader, the player can follow the Spin Jump up with a more useful maneuver or offensive ability. Again, the super transformation does not enhance the Spin Jump's size. It should be noted that when attacking Metal Madness with the Homing Attack, the player will bounce back in the Spin Jump formation.

    //To perform this move in gameplay, the player must press A Button GameCube v2.png/XboxA.png/PSXButton.png.

    //Swinging Spin Hammer Notes: To perform the Swinging Hammer Attack in gameplay, Team Rose must be in Speed Formation. The player then has to hold down the action button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) and release it without any teammates nearby to use the Swinging Hammer Attack.

    //Black Tornado Notes: When performing the Black Tornado, Shadow uses the Spin Dash to circle around an opponent in midair at high speed while leaving a yellow trail. The resulting slipstream creates a tornado effect that forms a black cyclonic vortex of air around the opponent.

    //The effect of the Black Tornado depends on the type of enemy it is used on.If used on unprotected Egg Pawns, Egg Knights or opposing playable teams, the Black Tornado sucks the opponents off their feet and flings them into the air, before they fall back down to the ground. Upon landing, they will take damage and be potentially stunned for a short time - for some enemies, this can even reveal their weak spots. If used on Egg Pawns or Egg Knights wearing shields, the Black Tornado will permanently blow off their shields, thus leaving them with a weaker defense.If used on larger or airborne enemies, the Black Tornado will make them disoriented at most, but will deal no damage.

    //Besides affecting enemies, the Black Tornado can also be used in conjunction with some of the gimmicks in Sonic Heroes, such as enabling the player to scale Poles or activate propellers.

    //To perform the Black Tornado in gameplay, Team Dark must be in Speed Formation. The player then has to press the action button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) during a jump to use the Black Tornado.When using this attack, Shadow will automatically home in on the nearest enemy within a reasonable distance before attacking. Unlike the Homing Attack, the Black Tornado will also home in on enemies that are above Shadow.

    //Blue Tornado with Notes: When performing the Blue Tornado, Sonic uses the Spin Dash to circle around an opponent in midair at high speed while leaving a blue trail. The resulting slipstream creates a cyclone effect that forms a blue cyclonic vortex of air around the opponent.

    //The effect of the Blue Tornado depends on the type of enemy it is used on.If used on unprotected Egg Pawns, Egg Knights or opposing playable teams, the Blue Tornado sucks the opponents off their feet and flings them into the air, before they fall back down to the ground. Upon landing, they will take damage and be potentially stunned for a short time - for some enemies, this can even reveal their weak spots. If used on Egg Pawns or Egg Knights wearing shields, the Blue Tornado will permanently blow off their shields, thus leaving them with a weaker defense.If used on larger or airborne enemies, the Blue Tornado will make them disoriented at most, but will deal no damage.

    //Besides affecting enemies, the Blue Tornado can also be used in conjunction with some of the gimmicks in Sonic Heroes, such as enabling the player to scale Poles or activate propellers.

    //To perform the Blue Tornado in gameplay, Team Sonic must be in Speed Formation. The player has to press the action button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) during a jump to use the Blue Tornado.When using this attack, Sonic will automatically home in on the nearest enemy in a certain distance before attacking. Unlike the Homing Attack, the Blue Tornado will also home in on enemies above Sonic.

    //When performing the Leaf Swirl, Espio jumps into the air and does a body flip while keeping his hands folded in front of his face. He then creates a tornado effect that forms a cyclonic vortex of air filled with transparent leaves around him. At the same time, Espio combines this with a ninja maneuver which invokes his innate ability to camouflage himself, turning him invisible.[1]

    //The effect of the Leaf Swirl depends on the type of enemy it is used on.If used on unprotected Egg Pawns, Egg Knights, or opposing playable teams, the Leaf Swirl sucks the opponents off their feet and flings them into the air, before they fall back down to the ground. Upon landing, they will take damage and be potentially stunned for a short time - for some enemies, this can even reveal their weak spots. If used on Egg Pawns or Egg Knights wearing shields, the Leaf Swirl will permanently blow off their shields, thus leaving them with a weaker defense.If used on larger or airborne enemies, the Leaf Swirl will make them disoriented at most, but will deal no damage.

    //In addition to affecting enemies, the Leaf Swirl makes Espio undetectable by enemies and the environment, which is signified by Espio turning transparent.While invisible, Espio cannot be detected by enemies or Giant Frogs, can pass through laser fields without getting hurt, and still attack enemies with his full moveset (aside from the Rocket Accel). However, Vector and Charmy will not follow him while he is invisible, and the player can still take damage.If the player takes damage, uses the Leaf Swirl once more, changes the leader or activates Chaotix Recital, Espio will become visible again.

    //Aside from the aforementioned effects, the Leaf Swirl can also be used in conjunction with some of the gimmicks in Sonic Heroes, such as enabling the player to scale Poles or activate propellers.Also, in stages such as Frog Forest and the Egg Fleet, Leaf Swirl lets Espio sneak past enemies and Giant Frogs unnoticed so as not to start the stage(s) all over again in case he is detected.

    //To perform the Leaf Swirl in gameplay, Team Chaotix must be in Speed Formation with Espio as the leader. The player then has to press the action button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) during a jump to use the Leaf Swirl.To use it on targets, Espio must be either near or above them.

    //Torndo Hammer, When performing this technique, Amy jumps into the air, where she pulls out her Piko Piko Hammer and swings it around herself once, creating a tornado effect that forms a cyclonic vortex of air filled with pink hearts that she can launch at her targets from afar. While using the Tornado Hammer, Amy gives off pink hearts.

    //The effect of the Tornado Hammer depends on the type of enemy it is used on.If used on unprotected Egg Pawns, Egg Knights or opposing playable teams, the Tornado Hammer sucks the opponents off their feet and flings them into the air, before they fall back down to the ground. Upon landing, they will take damage and be potentially stunned for a short time - for some enemies, this can even reveal their weak spots. If used on Egg Pawns or Egg Knights wearing shields, the Tornado Hammer will permanently blow off their shields, thus leaving them with a weaker defense.If used on larger or airborne enemies, the Tornado Hammer will make them disorientated at most, but will deal no damage.

    //Besides affecting enemies, the Tornado Hammer can also be used in conjunction with some of the gimmicks in Sonic Heroes, such as enabling the player to scale Poles or activate propellers.

    //To perform the Tornado Hammer in gameplay, Team Rose must be in Speed Formation. The player then has to press the action button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) during a jump to use the Tornado Hammer.To aim it, Amy has to be facing the target that the player wants to hit.
    public override void PowerAttack()
    {
        //Rocket Accel
        //Only When Grounded
    }

    public override void RegularAttack()
    {
        //Homing Attack (Lead By Sonic of Team Sonic)
        //Tornado Jump (Used by Sonic)
        //Triangle Jump (Used by all Speed Characters)
        //Light Speed Attack (Used by Sonic)
        //Light Speed Dash (Used by Sonic)
        //Propeller Hammer (Used by Amy Rose)
        //Spin (Used by all team members)
        //Spin Jump (Used by all team members)
        //Swinging Spin Hammer (Used By Amy Rose (Leader Of Team Rose)
        //Shuriken (Espio Leader Of Team Chaotix)
        //Blue Tornado (Only by Team Sonic)
        //Black Torndo (Only by Team Dark)
        //Leaf Swirl (Only used by Team Chotix Speed Character)
        //Tornado Hammer (Only used by Amy Rose in Team Rose)
        //Only when jumping and is released during midair
    } 
}
