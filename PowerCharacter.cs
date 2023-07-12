using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PowerCharacter : BaseCharacter
{
    //These comments are for Power Character Attacks

    //Fireball Jump: When performing the Fireball Jump, the user, while using Fire Combination, does a Spin Jump into the air and releases the Speed type and Fly type member on his team. While still maintaining their ball form, the Speed type and Fly type team member revolve around the user's own spinning form at high speed in a wide circle until the user lands on the ground again. While performing this move, any targets caught within the range of the rotating team members will take damage. //Note all Team Members use this

    //To perform the Fireball Jump in gameplay, the selected team must be in Power Formation with the Power type character as the leader, and the player must be performing Fire Combination.The player then has to press and hold down PSXButton.png/XboxA.png/A Button GameCube v2.png to execute the Fireball Jump. It can also be executed by pressing XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png in Power Formation while airborne.

    //Foward Power Attack: When performing the Forward Power Attack, the Power type character on the team attacks enemies directly ahead with a forward strike. Each team's Forward Power Attack differs between each other in terms of performance:

    //Team Sonic: Knuckles grabs Tails in his right hand as he curls up into a spinball form and throws a powerful forward punch with his right fist while using Tails as a melee weapon.This is called the Dash Punch.

    //Team Dark: Omega grabs Rouge in his right hand as she curls up into a spinball form and retracts his right hand so Rouge rests in his wrist socket.He then throws a forward vertical punch with his right arm while using Rouge as a melee weapon.This is called the Dash Punch.

    //Team Rose: Big pulls out his fishing rod with his left hand and uses it as a club to knock his targets. This is called the Umbrella Attack.

    //Team Chaotix: Vector thrusts his head forward and crushes his targets with his enormous jaws.

    //To perform this technique in gameplay, the selected team must be in Power Formation with the Power type member as the leader. The player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png to execute the Forward Power Attack.

    //Hammer Down(Team Chotix): When performing the Hammer Down, Vector jumps into the air, where he puts his hands together into a collective fist above his head. As he then falls down, Vector thrusts his collective fist down on the opponent beneath him while putting his entire weight into the strike, hitting with enough force to smash through metal and create fiery shockwaves on the ground upon impact. While falling down, Vector gives off a green aura.

    //When using Auto Homing, the Power type character of the team approaches nearby enemies. The Speed type member and Fly type member on the team then jump up and attack the enemies with the Homing Attack.

    //Notes: All Team Members use this attack: To perform the Auto Homing in gameplay, the selected team has to be in Power Formation and must have as least one other teammate present or the technique will fail.All the player has to do is approach the enemies and the other teammates will automatically attack them with the Homing Attack.They will also attack the enemies while the Power type character is in motion.

    //When the Speed type member attack as a part of Auto Homing, the effects of their Homing Attack will match the level the Speed type member is currently on; if the Speed type member has three blue Power Cores, then the speed type member's Homing Attack uses due to Auto Homing will have the same power and effects of a level 3 Homing Attack as well.

    //Notes: Big The Cat Uses Only: When performing the Body Press, Big jumps into the air while spreading out his arms and legs, and pointing his belly straight down.Big then brings his entire body weight slamming down towards the ground with enough force to create fiery shockwaves on the ground.While falling down, Big gives off a purple aura.

    //Notes: Vector (Team Chaotix) only uses this: When performing Bubblegum Descent, Vector inflates a massive purple balloon-bubble made of bubblegum while in midair, which slows his own descent by catching the air beneath him. Using this ability around wind currents or fans allows Vector to accelerate in the direction of the winds. While descending, Vector gives off a green aura and can damage unarmed Badniks by knocking into them.

    //To perform the Body Press in gameplay, Team Rose must be in Power Formation. The player then has to press the action button (PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png) during the jump to execute the Body Press.Noticeably, after hitting a surface with the Body Press, Big will bounce slightly back into the air. Also, the Body Press can be used repeatedly, either by pressing the action button again when Big bounces back into the air or automatically after damaging an opponent with the Body Press, but not destroying it. When used, the Body Press can be interrupted by either taking damage or using Umbrella Descent during its execution.

    //Jump Fire Knock (Big The Cat) (Team Rose) When using Jump Fire Knock, Big enters Fire Combination and jumps into the air where he has either Amy or Cream Spin Jump off his shoulders. Big then uses his Fishing Rod to knock the teammate in baseball-style diagonally towards the ground with such force that the teammate turns into a fireball and creates an explosion upon impact that damage everything in the vicinity. This allows the player to damage enemies, break down doors and destroy obstacles from above.

    //To perform this technique in gameplay, the player must have Team Rose in Power Formation with Big as the leader. The player then has to enter Fire Combination and jump into the air, where the player has to press the action button (XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png) to fire the teammates. When used near targets, the thrown teammates will automatically home in on them. Additionally, the Jump Fire Knock can be used up to two times in a row, depending on the number of teammates Big carries.

    //Vector (Team Chaotix) When using Jump Fireball, Vector, having entered Fire Combination beforehand and thus holding Espio and Charmy as spinning balls in his jaws, jumps into the air. He then bends his head back before spiting out one of his teammates diagonally towards the ground with such force that the teammate turns into a fireball and creates an explosion upon impact that damage everything in the vicinity. This allows the player to damage enemies, break open doors and destroy obstacles from above.

    //All Teams Use This Attack: When performing the Remote Power Attack, the Power type character on the team attacks surrounding enemies with a wide-ranged attack. Each team's Forward Power Attack differs between each other in terms of performance:

    //Team Sonic: While holding Sonic in his right hand as he is curled up into a spinball form, Knuckles punches into the ground with his right fist while holding Sonic, creating a burst of fire upon impact.This is called the Spinning Back punch.

    //Team Dark: While holding Shadow in his left wrist socket, Omega throws a vertical punch with his left arm while holding Shadow, releasing a burst of fire in his wake. This is called the Spinning Back punch.

    //Team Rose: Big grabs his teammates in his shoulders and knocks them away with his fishing rod as destructive balls of fire. This is called the Fire Knock.

    //Team Chaotix: Vector grabs his spinning teammates in his jaws and splits them out as destructive balls of fire. This is called the Fireball.

    //Fire Knock (Notes): only Big The Cat from Team Rose uses this, When performing the Fire Knock, Big, having entered Fire Combination before-handed, has one of his teammates Spin Jump off his shoulder. Big then uses his Fishing Rod to knock his teammate in baseball-style at his targets with such force that she turns into a destructive fireball. This allows the player to damage enemies, break down doors and destroy other obstacles that can be on both the ground and in midair from afar.

    //To perform this technique in gameplay, the player must have Team Rose in Power Formation with Big as the leader.The player then has to enter Fire Combination and press the action button (XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png) to fire the teammates.When used near targets, the thrown teammates will automatically home in on them. Additionally, the Fire Knock can be used up to two times in a row, depending on the number of teammates Big carries.

    //To perform the Remote Power Attack in gameplay, the selected team must in Power Formation with the Power type member as the leader and the player must have performed the Forward Power Attack. Right after using the Forward Power Attack, the player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png to use the Remote Power Attack.

    //Only Knuckles (Team Sonic and E-123 Omega (Team Dark) uses this, When performing the Triangle Dive, all three members on the team arrange themselves in midair into a triangle shape. They then put their hands together in the center of the formation, allowing them to catch the air beneath their bodies and slow their descent. While descending, each team member gives off their respective aura.

    //While using the Triangle Dive, the player is able to greatly slow their descent through midair and as well slowly move in all directions, similar to Glide. Also, in areas with vertical drafts (e.g. fans), the player is able to catch the upward draft generated at these places and ascend to higher sections by using the Triangle Dive above the source of these updrafts.

    //To perform the Triangle Dive, the selected character has to be in Power Formation with Power type member (Knuckles the Echidna or E-123 Omega) as the leader.The player must then press and hold down the jump button (PSXButton.png/XboxA.png/A Button GameCube v2.png) while jumping or in midair and can as well use the control stick to move in different directions. To break the Triangle Dive, the player has to release the jump button.

    //Notes:Big The Cat (Team Rose) Only uses this skillWhen performing the Umbrella Descent, Big pulls out his Fishing Rod in midair and folds outs its umbrella runner, allowing Big to catch the air beneath him slow his own descend. Meanwhile, Amy and Cream grab onto Big's belt to ride with him. While descending, Big gives off a purple aura.

    //While using the Umbrella Descent, the player is able to greatly slow his/her descend through midair and as well slowly move in all directions, similar to Glide. Also, in areas with vertical drafts (e.g. fans), the player is able to catch the upward draft generated at these places and ascend to higher sections by using the Umbrella Descent above the source of these updrafts.

    //To perform the Umbrella Descent in gameplay, Team Rose has to be in Power Formation with Big as the leader. The player must then press and hold down the jump button (PSXButton.png/XboxA.png/A Button GameCube v2.png) while jumping or in midair and can as well use the control stick to move in different directions. To break the Umbrella Descent, the player has to release the jump button.

    //Note: All Teams use this: When performing the Wide Power Attack, the Power type character on the team breaks out a final area-of-effect move in all directions that damages everything caught within it. Each team's Wide Power Attack differs from each other in terms of performance and effects:

    //Team Sonic: Knuckles launches a single punch into the ground, causing shockwaves to erupt nearby.This is called the Volcanic Dunk.

    //Team Dark: Omega uses concealed weaponry in his arms and shoots them in a circle. This is called the Omega Arm.

    //Team Rose: Big pulls out his Fishing Rod and starts swinging his fishing reel around dangerously.This is called Big Fishing.

    //Team Chaotix: Vector falls down and spins around on his tail as he uses his putrid breath to knock enemies into submission. This is called Vector Breath.

    //To perform the Wide Power Attack in gameplay, the selected team must be in Power Formation, and the player must have completed the Remote Power Attack. Right after finishing the Remote Power Attack, the player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png to attack with the Wide Power Attack.

    //When performing the Big Fishing, Big stands firm in one place and raises his fishing rod as high as he can above himself. He then reels out his fishing rod's lure, which is attached to his fishing rod's line, and starts swinging it around dangerously in a circular motion damaging anything caught within its radius.

    //By collecting red Power Cores during gameplay, the player can increase the power, nature, and radius of the Big Fishing.The levels of power are as follows:

    //Level 1: Big pulls out his fishing rod and swings a burning, oversized red lure around in a circle.

    //Level 2: Big pulls out his fishing rod and swings a burning orange and white striped lifebuoy with anchor symbols around in a circle. Attack radius is slightly increased and the resulting damage dealt is doubled.

    //Level 3: Big pulls out his fishing rod and swings a large, burning metal mace with spikes around in a circle. Attack radius and damage is at maximum.

    //To perform this technique in gameplay, the player must have Team Rose in Power Formation and must have performed the Fire Knock.Right after finishing the Fire Knock, the player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png to use the Big Fishing. While using Big Fishing, the player can jump to prematurely end the move.

    //When performing Omega Arm, Omega releases his teammates from his grip (due to holding them for the move's initial attacks) and stands in one place while spinning his robotic torso around clockwise. While doing so, Omega unleashes a full-force weapon barrage from his built-in arm cannons, which he transforms according to the weapon he uses. This results in a spiral-like wave of artillery that damages anything caught within its radius.

    //By collecting red Power Cores during gameplay, the player can increase the power, nature, and radius of the Omega Arm.The levels of power are as follows:

    //Level 1: Omega reconfigures his claws into rotary cannons and fires a hail of bullets in a circular radius around him.

    //Level 2: Omega reconfigures his claws into Flamethrowers, spraying flames in a circular radius around him.Attack radius is increased about a couple of meters and the resulting damage dealt is doubled.

    //Level 3: Omega reconfigures his claws into Missile Launchers, and unleashes a barrage of drill-like missiles in a wide circular arc that create explosions upon contact with nearby enemies or obstacles.Attack radius and damage is at maximum. The missiles fire at a slightly upward angle so that they may not hit ground enemies from a far distance.

    //To perform this technique in gameplay, the player must have Team Dark in Power Formation and must have performed the Spinning Back punch. Right after using the Spinning Back punch, the player then has to press XboxX.png/PSSquareButton.png/SNNBGAMECUBEDISCO.png to use the Omega Arm.

    //Note: only Vector uses: When performing Vector Breath, Vector falls down on his tail and balances on it slightly above the ground. Vector then puts his head down, facing forward, and does a 360-degree turn on his tail while using a powerful mouth breath that damages anything caught within its radius.

    //When performing Volcanic Dunk, Knuckles jumps slightly into the air while releasing his teammates from his grip (due to holding them for the move's initial attacks). He then punches into the ground with his fist with such force that it creates powerful volcanic explosions which damage anything caught within its radius.

    //By collecting red Power Cores during gameplay, the player can increase the power, nature, and radius of the Volcanic Dunk.The levels of power are as follows:

    //Level 1: Knuckles punches into the ground and creates a slight tremor, releasing a flaming shockwave.

    //Level 2: Knuckles punches into the ground, which forms a bigger flaming shockwave and creates a bigger tremor.Attack radius is increased about a couple of meters and the resulting damage dealt is doubled.

    //Level 3: Knuckles punches into the ground, creating a powerful volcanic eruption for a few seconds which makes fireballs spew out from the ground and high in the air.The explosions the move create are capable of defeating almost any enemy they touch.Attack radius and damage is at maximum.

    //To perform this technique in gameplay, the player must have Team Sonic in Power Formation and must have performed the Spinning Back punch. Right after using the Spinning Back punch, the player then has to press PSSquareButton.png/XboxX.png/SNNBGAMECUBEDISCO.png to use the Volcanic Dunk.

    //During the battle with Metal Overlord, when Super Knuckles is performing the Volcanic Dunk, he will punch continuously into the air while releasing a fury of fireballs that explode upon contact. As long as the action button is repeatedly pressed, the player can perform this attack for as long as desired
}


