# dfunity-critical-strikes
DFU Mod - Critical Strikes inflict extra damage

### Description

This is a simple mod that changes the behaviour of critical hits to inflict extra damage, instead of simply adding extra chance to hit.

Right now the values are fixed, but I plan on adding certain level of configurability though settings.

### Motivation

I prefer that critical strikes do extra damage, it seems the most common implementation of this skill in games. Besides, the original implementation made the skill had very small impact.

There are a couple of mods that have the option to have similar functionality, but those are large mods that change several other systems in the game. I wanted to make a simple mod that you can use with other without issues.

### Compatibility

Should be compatible as long as other mods don't override the CalculateAttackDamage method.
