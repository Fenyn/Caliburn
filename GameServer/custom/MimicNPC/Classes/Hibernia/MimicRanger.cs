﻿using DOL.GS.PlayerClass;
using log4net;
using System.Reflection;

namespace DOL.GS.Scripts
{
    public class MimicRanger : MimicNPC
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MimicRanger(byte level) : base(new ClassRanger(), level)
        {
            MimicSpec = new RangerSpec();

            DistributeSkillPoints();
            MimicEquipment.SetRangedWeapon(this, eObjectType.RecurvedBow);
            MimicEquipment.SetMeleeWeapon(this, MimicSpec.WeaponTypeOne, eHand.oneHand);
            MimicEquipment.SetMeleeWeapon(this, MimicSpec.WeaponTypeOne, eHand.leftHand);
            MimicEquipment.SetArmor(this, eObjectType.Reinforced);
            MimicEquipment.SetJewelry(this);
            RefreshItemBonuses();
            SwitchWeapon(eActiveWeaponSlot.Standard);
            RefreshSpecDependantSkills(false);
            SetSpells();
            IsCloakHoodUp = Util.RandomBool();
        }
    }

    public class RangerSpec : MimicSpec
    {
        public RangerSpec()
        {
            SpecName = "RangerSpec";
            is2H = false;

            int randBaseWeap = Util.Random(1);

            switch (randBaseWeap)
            {
                case 0: WeaponTypeOne = "Blades"; break;
                case 1: WeaponTypeOne = "Piercing"; break;
            }

            int randVariance = Util.Random(7);

            switch (randVariance)
            {
                case 0:
                case 1:
                Add(WeaponTypeOne, 32, 0.4f);
                Add("Recurve Bow", 35, 0.9f);
                Add("Pathfinding", 40, 0.5f);
                Add("Celtic Dual", 29, 0.3f);
                Add("Stealth", 35, 0.2f);
                break;

                case 2:
                case 3:
                Add(WeaponTypeOne, 35, 0.4f);
                Add("Recurve Bow", 35, 0.9f);
                Add("Pathfinding", 36, 0.5f);
                Add("Celtic Dual", 31, 0.3f);
                Add("Stealth", 35, 0.2f);
                break;

                case 4:
                case 5:
                Add(WeaponTypeOne, 27, 0.4f);
                Add("Recurve Bow", 45, 0.9f);
                Add("Pathfinding", 40, 0.5f);
                Add("Celtic Dual", 19, 0.3f);
                Add("Stealth", 35, 0.2f);
                break;

                case 6:
                Add(WeaponTypeOne, 35, 0.6f);
                Add("Pathfinding", 42, 0.5f);
                Add("Celtic Dual", 40, 1.0f);
                Add("Stealth", 35, 0.2f);
                break;

                case 7:
                Add(WeaponTypeOne, 25, 0.6f);
                Add("Pathfinding", 40, 0.5f);
                Add("Celtic Dual", 50, 1.0f);
                Add("Stealth", 33, 0.2f);
                break;
            }
        }
    }
}