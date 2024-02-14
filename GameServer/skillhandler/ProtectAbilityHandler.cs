using System.Linq;
using System.Reflection;
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System.Reflection;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;

namespace DOL.GS.SkillHandler
{
    [SkillHandlerAttribute(Abilities.Protect)]
    public class ProtectAbilityHandler : IAbilityActionHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const int PROTECT_DISTANCE = 1000;

        public void Execute(Ability ab, GamePlayer player)
        {
            if (player == null)
            {
                if (log.IsWarnEnabled)
                    log.Warn("Could not retrieve player in ProtectAbilityHandler.");

                return;
            }

            if (player.TargetObject is not GameLiving target)
            {
                foreach (ProtectECSGameEffect protect in player.effectListComponent.GetAbilityEffects().Where(e => e.EffectType == eEffect.Protect))
                {
                    if (protect.Source == player)
                        EffectService.RequestCancelEffect(protect);
                }

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Protect.CancelTargetNull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (target == player)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Protect.CannotUse.CantProtectYourself"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Group group = player.Group;

            if (group == null || !group.IsInTheGroup(target))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Protect.CannotUse.NotInGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            CheckExistingEffectsOnTarget(player, target, true, out bool foundOurEffect, out ProtectECSGameEffect existingEffectFromAnotherSource);

            if (foundOurEffect)
                return;

            if (existingEffectFromAnotherSource != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Protect.CannotUse.ProtectTargetAlreadyProtectEffect", existingEffectFromAnotherSource.Source.GetName(0, true), existingEffectFromAnotherSource.Target.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            CancelOurEffectThenAddOnTarget(player, target);
        }

        public static void CheckExistingEffectsOnTarget(GameLiving source, GameLiving target, bool cancelOurs, out bool foundOurEffect, out ProtectECSGameEffect effectFromAnotherSource)
        {
            foundOurEffect = false;
            effectFromAnotherSource = null;

            foreach (ProtectECSGameEffect protect in target.effectListComponent.GetAbilityEffects().Where(e => e.EffectType == eEffect.Protect))
            {
                if (protect.Source == source)
                {
                    foundOurEffect = true;

                    if (cancelOurs)
                        EffectService.RequestCancelEffect(protect);
                }

                if (protect.Target == target)
                    effectFromAnotherSource = protect;
            }
        }

        public static void CancelOurEffectThenAddOnTarget(GameLiving source, GameLiving target)
        {
            foreach (ProtectECSGameEffect protect in source.effectListComponent.GetAbilityEffects().Where(e => e.EffectType == eEffect.Protect))
            {
                if (protect.Source == source)
                    EffectService.RequestCancelEffect(protect);
            }

            new ProtectECSGameEffect(new ECSGameEffectInitParams(source, 0, 1, null), source, target);
        }
    }
}