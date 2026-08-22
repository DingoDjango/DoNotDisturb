using UnityEngine;
using Verse;

namespace Do_Not_Disturb
{
	public class Main : Mod
	{
		public Main(ModContentPack content) : base(content)
		{
			this.GetSettings<Settings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);

			Settings.DoSettingsWindowContents(inRect.LeftPart(0.75f));
		}

		public override string SettingsCategory()
		{
			return "Do Not Disturb";
		}
	}
}
