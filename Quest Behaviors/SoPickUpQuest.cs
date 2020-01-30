﻿using Clio.XmlEngine;
using ff14bot.RemoteWindows;
using System.ComponentModel;
using TreeSharp;
using Action = TreeSharp.Action;

namespace ff14bot.NeoProfiles.Tags
{
    [XmlElement("SoPickUpQuest")]
    [XmlElement("SoPickupQuest")]
    class SoPickUpQuest : PickupQuestTag
    {
        [DefaultValue(0)]
        [XmlAttribute("DialogOption")]
        public int DialogOption { get; set; }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                new Decorator(ret => SelectString.IsOpen,
                    new Action(r =>
                    {
                        SelectString.ClickSlot((uint)DialogOption);
                    })
                ),
                base.CreateBehavior()
            );
        }
    }
}
