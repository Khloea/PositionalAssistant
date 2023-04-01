namespace PrincessRTFM.PositionalGuide;

using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;

using ImGuiNET;

public class ConfigWindow: Window, IDisposable {
	public const float InactiveOptionAlpha = 0.5f;

	private const ImGuiWindowFlags flags = ImGuiWindowFlags.None
		| ImGuiWindowFlags.NoScrollbar
		| ImGuiWindowFlags.NoScrollWithMouse
		| ImGuiWindowFlags.AlwaysAutoResize;
	private const int ptrMemWidth = sizeof(short);

	private readonly IntPtr stepPtr;
	private readonly IntPtr minBoundingPtr;
	private readonly IntPtr maxBoundingPtr;
	private readonly IntPtr minModifierPtr;
	private readonly IntPtr maxModifierPtr;
	private readonly IntPtr minThicknessPtr;
	private readonly IntPtr maxThicknessPtr;
	private readonly IntPtr negativeOnePtr;
	private readonly IntPtr negativeTwoPtr;
	private readonly IntPtr maxTetherLengthPtr;

	private bool disposed;

	private readonly Configuration conf;

	public ConfigWindow(Plugin core) : base(core.Name, flags) {
		this.RespectCloseHotkey = true;
		this.conf = core.Config;
		this.stepPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.minBoundingPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxBoundingPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.minModifierPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxModifierPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.minThicknessPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxThicknessPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.negativeOnePtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.negativeTwoPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxTetherLengthPtr = Marshal.AllocHGlobal(ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)1), 0, this.stepPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)byte.MinValue), 0, this.minBoundingPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)byte.MaxValue), 0, this.maxBoundingPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)sbyte.MinValue), 0, this.minModifierPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)sbyte.MaxValue), 0, this.maxModifierPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)1), 0, this.minThicknessPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)5), 0, this.maxThicknessPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)-1), 0, this.negativeOnePtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)-2), 0, this.negativeTwoPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)32), 0, this.maxTetherLengthPtr, ptrMemWidth);
	}

	public override void Draw() {
		float scale = ImGuiHelpers.GlobalScale;
		ImGuitils utils = new(500);
		bool changed = false;

		bool[] drawing = this.conf.DrawGuides;
		bool[] limitRender = new bool[] {
			this.conf.OnlyRenderWhenEitherEndOnScreen,
			this.conf.OnlyRenderWhenCentreOnScreen,
			this.conf.OnlyRenderWhenEndpointOnScreen,
		};
		Vector4[] colours = this.conf.LineColours;

		// used to chunk-copy values for the sliders in the config window, since ImGui uses pointers to the values
		short[] hack = new short[] {
			this.conf.ExtraDrawRange,
			this.conf.MinDrawRange,
			this.conf.MaxDrawRange,
			this.conf.LineThickness,
			this.conf.TetherLengthInner,
			this.conf.TetherLengthOuter,
		};
		IntPtr[] ptrs = new IntPtr[hack.Length];
		for (int i = 0; i < hack.Length; ++i) {
			ptrs[i] = Marshal.AllocHGlobal(ptrMemWidth);
			Marshal.Copy(BitConverter.GetBytes(hack[i]), 0, ptrs[i], ptrMemWidth);
		}

		bool active = this.conf.Enabled;
		bool tether = this.conf.DrawTetherLine;
		bool flattenTether = this.conf.FlattenTether;
		bool players = this.conf.DrawOnPlayers;
		bool onlySelf = this.conf.DrawOnSelfOnly;
		Vector4 tetherColour = this.conf.TetherColour;

		// this is where the rendering starts, with a pvp warning
		if (Plugin.Client.IsPvP) {
			ImGui.TextUnformatted("You are currently in a PvP zone.");
			ImGui.TextUnformatted("This plugin does not function in PvP.");
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
		}

		// simple singular values...
		changed |= ImGui.Checkbox("Enabled?", ref active);
		utils.Tooltip("Whether to draw any lines at all");

#if DEBUG
		// Not available outside of debug mode because there's no point except for debugging
		// If you REALLY want to force it on, it's in the config file, just not the settings window
		// Still won't affect PvP though, so I don't see why you'd bother
		changed |= ImGui.Checkbox("Players?", ref players);
		utils.Tooltip("Should guides be drawn on players?"
			+ "\n"
			+ "\nThis is a debug-build exclusive setting. PvP still isn't enabled.");
		ImGui.PushStyleVar(ImGuiStyleVar.Alpha, players ? 1 : InactiveOptionAlpha);
		ImGui.Indent();
		changed |= ImGui.Checkbox("Self only?", ref onlySelf);
		ImGui.Unindent();
		utils.Tooltip("If enabled, guides will only be drawn on yourself."
			+ " This will make debugging easier by letting you leave this option enabled,"
			+ " while still not cluttering your screen when you target other players.");
		ImGui.PopStyleVar();
#endif

		changed |= ImGui.Checkbox("Tether line?", ref tether);
		utils.Tooltip("Should a line be drawn connecting you to your target?");
		ImGui.SameLine();
		changed |= ImGui.ColorEdit4("Line colour for the tether", ref tetherColour, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

		ImGui.PushStyleVar(ImGuiStyleVar.Alpha, tether ? 1 : InactiveOptionAlpha);
		ImGui.Indent();
		changed |= ImGui.Checkbox("Two dimensional?", ref flattenTether);
		utils.Tooltip("If enabled, the tether will be drawn entirely on the same plane as your target,"
			+ " even if your height is different. This is recommended if you intend to limit the tether length.");
		ImGui.Unindent();
		ImGui.PopStyleVar();

		// now we do the 3x3 for guidelines and the target ring
		ImGui.TextUnformatted("\nWhich guidelines do you want, and in what colours?");
		foreach (int i in new int[] { 7, 0, 1, 6, 2, 5, 4, 3 }) { // awful ugly hack, sorry
			changed |= ImGui.Checkbox($"###drawGuide{i}", ref drawing[i]);
			utils.Tooltip($"Draw the {Configuration.Directions[i]} guideline?");
			ImGui.SameLine();
			changed |= ImGui.ColorEdit4($"Line colour for {Configuration.Directions[i]}", ref colours[i], ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
			if (i is 7 or 5) // awful ugly hack (continuation)
				ImGui.SameLine(80 * scale);
			else if (i is 0 or 6 or 4)
				ImGui.SameLine(160 * scale);
			else if (i is not 3)
				ImGuiHelpers.ScaledDummy(30);
		}

		// some more fairly simple singular(ish) values again
		changed |= ImGui.Checkbox("Only draw lines when at least one of the points is on screen?", ref limitRender[0]);
		utils.Tooltip("Some users have reported that lines being partially off-screen causes a visual bug, wherein the line just shoots off across your whole screen in a random direction."
				+ " This setting will stop individual lines from being drawn if neither point is on the screen."
				+ "\n"
				+ "\nThere may still be some weirdness in certain camera angles, but it should be much more limited."
				+ "\n"
				+ "\nThe following two options only apply if this one is disabled.");

		ImGui.PushStyleVar(ImGuiStyleVar.Alpha, limitRender[0] ? InactiveOptionAlpha : 1);

		ImGui.Indent();
		changed |= ImGui.Checkbox("Only draw lines when the target's centrepoint is on the screen?", ref limitRender[1]);
		ImGui.Unindent();

		utils.Tooltip("This setting will stop individual lines from being drawn if the inner point (the middle of the target's hitbox) isn't on your screen."
			+ "\n"
			+ "\nOnly applies if the above override option is disabled.");

		ImGui.Indent();
		changed |= ImGui.Checkbox("Only draw lines when the line's endpoint is on the screen?", ref limitRender[2]);
		ImGui.Unindent();

		utils.Tooltip("Much like the above, this setting stops lines from drawing if their endpoint (at/past the outside of the target's hitbox) isn't on your screen."
				+ "\n"
				+ "\nOnly applies if the above override option is disabled.");

		ImGui.PopStyleVar();

		// sliders for numeric modifiers to the lines being drawn
		ImGui.PushItemWidth(470 * scale);

		changed |= ImGui.SliderScalar("Guideline size modifier", ImGuiDataType.S16, ptrs[0], this.minModifierPtr, this.maxModifierPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("The positional guidelines normally extend from the target's centre point out to the size of their hitbox. This allows you to make them longer or shorter.");

		changed |= ImGui.SliderScalar("Minimum guideline size", ImGuiDataType.U16, ptrs[1], this.minBoundingPtr, this.maxBoundingPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("Guidelines will always be drawn to at least this length, even if they would ordinarily have been smaller.");

		changed |= ImGui.SliderScalar("Maximum guideline size", ImGuiDataType.U16, ptrs[2], this.minBoundingPtr, this.maxBoundingPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("Guidelines will never be longer than this, no matter how big the target's hitbox is.");

		changed |= ImGui.SliderScalar("Line thickness", ImGuiDataType.U16, ptrs[3], this.minThicknessPtr, this.maxThicknessPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("How wide/thick do you want the lines to be?");

		// sliders specifically to control the tether line length
		ImGui.PushStyleVar(ImGuiStyleVar.Alpha, tether ? 1 : InactiveOptionAlpha);

		changed |= ImGui.SliderScalar("Tether inner max length", ImGuiDataType.S16, ptrs[4], this.negativeOnePtr, this.maxTetherLengthPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("The inner tether will never extend beyond the centre of the target's hitbox."
			+ "\n"
			+ "\nSet to -1 to always go to the centre of the target's hitbox.");

		changed |= ImGui.SliderScalar("Tether outer max length", ImGuiDataType.S16, ptrs[5], this.negativeTwoPtr, this.maxTetherLengthPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("The outer tether CAN extend beyond your hitbox."
			+ "\n"
			+ "\nSet to -1 to always go to the centre of the your hitbox."
			+ "\nSet to -2 to always go to the edge of your hitbox.");

		ImGui.PopStyleVar();
		ImGui.PopItemWidth();

		// empty text lines as spacers around the separator line
		ImGui.TextUnformatted("");
		ImGui.Separator();
		ImGui.TextUnformatted("");

		if (ImGui.CollapsingHeader("Command Help")) {
			ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
			ImGui.TextUnformatted("Most of the above toggle settings can be changed via command:");
			ImGui.Indent();
			ImGui.TextUnformatted($"{Plugin.Command} <action> [target]");
			ImGui.Unindent();
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Action may be any of the following:");
			ImGui.Indent();
			ImGui.TextUnformatted("enable, disable, toggle");
			ImGui.Unindent();
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Target may be any of the following, hyphyens optional, to toggle that line:");
			ImGui.Indent();
			ImGui.TextUnformatted("fl, front-left, f, front, fr, front-right, r, right, br, back-right, b, back, bl, back-left, l, left");
			ImGui.Unindent();
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Additionally, you can use 'cardinal', 'cardinals', 'diagonal', 'diagonals', and 'all' to affect multiple lines with a single command.");
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Finally, you can use the target 'tether' to toggle tether line rendering, and the target 'render' (the default) to toggle showing guides at all without losing your settings.");
			ImGui.PopTextWrapPos();
		}

		if (changed) {
			for (int i = 0; i < hack.Length; ++i)
				Marshal.Copy(ptrs[i], hack, i, 1);
			this.conf.Enabled = active;
			this.conf.DrawOnPlayers = players;
			this.conf.DrawTetherLine = tether;
			this.conf.FlattenTether = flattenTether;
			this.conf.TetherColour = tetherColour;
			this.conf.OnlyRenderWhenEitherEndOnScreen = limitRender[0];
			this.conf.OnlyRenderWhenCentreOnScreen = limitRender[1];
			this.conf.OnlyRenderWhenEndpointOnScreen = limitRender[2];
			this.conf.ExtraDrawRange = hack[0];
			this.conf.MinDrawRange = hack[1];
			this.conf.MaxDrawRange = hack[2];
			this.conf.LineThickness = hack[3];
			this.conf.TetherLengthInner = hack[4];
			this.conf.TetherLengthOuter = hack[5];
			this.conf.DrawGuides = drawing;
			this.conf.LineColours = colours;
			Plugin.Interface.SavePluginConfig(this.conf);
		}

		for (int i = 0; i < ptrs.Length; ++i)
			Marshal.FreeHGlobal(ptrs[i]);
	}

	#region Disposable
	protected virtual void Dispose(bool disposing) {
		if (this.disposed)
			return;
		this.disposed = true;

		if (disposing) {
			// nop
		}

		Marshal.FreeHGlobal(this.stepPtr);
		Marshal.FreeHGlobal(this.minBoundingPtr);
		Marshal.FreeHGlobal(this.maxBoundingPtr);
		Marshal.FreeHGlobal(this.minModifierPtr);
		Marshal.FreeHGlobal(this.maxModifierPtr);
		Marshal.FreeHGlobal(this.minThicknessPtr);
		Marshal.FreeHGlobal(this.maxThicknessPtr);
		Marshal.FreeHGlobal(this.negativeOnePtr);
		Marshal.FreeHGlobal(this.negativeTwoPtr);
		Marshal.FreeHGlobal(this.maxTetherLengthPtr);
	}

	~ConfigWindow() {
		this.Dispose(false);
	}

	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
