using ProjectIGI.Game.Config;
using ProjectIGI.Input;

namespace ProjectIGI.Game.Player;

/// <summary>A title-specific action and its initial keyboard or mouse assignment.</summary>
/// <param name="Mask">The action's bit in the title's input mask.</param>
/// <param name="Default">The initial profile binding, also naming the persisted action.</param>
/// <param name="Label">The label shown in controls settings.</param>
/// <param name="Replaces">A shared action superseded by this title's own control.</param>
public readonly record struct TitleInputBinding(ulong Mask, InputBinding Default, string? Label = null, PlayerAction Replaces = 0);

/// <summary>Feeds additional title controls through the same profile bindings as shared actions.</summary>
public static class TitleInputBindings
{
    /// <summary>Adds missing controls without replacing saved assignments or occupying an assigned key.</summary>
    /// <param name="profile">The profile to extend.</param>
    /// <param name="bindings">The title's additional controls.</param>
    /// <remarks>
    /// A row an earlier build left behind unbound is repaired to the title's default, not left dead:
    /// a profile saved before a default key existed keeps the old unbound row (prone shipped unbound
    /// before it was given KEY_X), and only adding missing rows would leave that button silent for
    /// ever. A row the player rebound to something usable is left exactly as they set it.
    /// </remarks>
    public static void RestoreMissing(PlayerProfile profile, IReadOnlyList<TitleInputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bindings);
        foreach (TitleInputBinding binding in bindings)
        {
            int index = profile.Bindings.FindIndex(b => string.Equals(b.Action, binding.Default.Action, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                if (!HasUsableRow(profile, binding.Default.Action))
                {
                    profile.Bindings[index] = UnboundReplacement(profile, bindings, binding);
                }

                continue;
            }

            profile.Bindings.Add(UnboundReplacement(profile, bindings, binding));
        }
    }

    /// <summary>The binding a control gets when it has none usable: its default, or unbound when another visible control already holds that key.</summary>
    private static InputBinding UnboundReplacement(PlayerProfile profile, IReadOnlyList<TitleInputBinding> bindings, TitleInputBinding binding)
    {
        bool occupied = profile.Bindings.Exists(b => Label(b.Action, bindings) is not null
            && string.Equals(b.Device, binding.Default.Device, StringComparison.OrdinalIgnoreCase)
            && string.Equals(b.Key, binding.Default.Key, StringComparison.OrdinalIgnoreCase));
        return occupied ? binding.Default with { Key = PlayerProfile.UnboundKey } : binding.Default;
    }

    /// <summary>Gets the shared controls superseded by the title's own bindings.</summary>
    /// <param name="bindings">The title's controls.</param>
    /// <returns>The shared action mask to suppress.</returns>
    public static PlayerAction ReplacedActions(IReadOnlyList<TitleInputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        PlayerAction mask = 0;
        foreach (TitleInputBinding binding in bindings) { mask |= binding.Replaces; }
        return mask;
    }

    /// <summary>Names a visible control, hiding controls belonging to another title or superseded shared actions.</summary>
    /// <param name="action">The stored profile action.</param>
    /// <param name="bindings">The running title's controls.</param>
    /// <returns>The settings label, or null for an inactive control.</returns>
    public static string? Label(string action, IReadOnlyList<TitleInputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(bindings);
        foreach (TitleInputBinding binding in bindings)
        {
            if (string.Equals(binding.Default.Action, action, StringComparison.OrdinalIgnoreCase)) { return binding.Label ?? action; }
            if (binding.Replaces != 0 && string.Equals(PlayerInputBindings.ConfigName(binding.Replaces), action, StringComparison.OrdinalIgnoreCase)) { return null; }
        }
        return action.StartsWith("Title_", StringComparison.OrdinalIgnoreCase) ? null : action;
    }

    /// <summary>Resets the title's bindings while leaving other titles' assignments intact.</summary>
    /// <param name="profile">The profile to reset.</param>
    /// <param name="bindings">The running title's defaults.</param>
    public static void Reset(PlayerProfile profile, IReadOnlyList<TitleInputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bindings);
        foreach (InputBinding shared in PlayerProfile.DefaultBindings)
        {
            if (Label(shared.Action, bindings) is null) { continue; }
            int index = profile.Bindings.FindIndex(b => string.Equals(b.Action, shared.Action, StringComparison.OrdinalIgnoreCase));
            if (index < 0) { profile.Bindings.Add(shared); }
            else { profile.Bindings[index] = shared; }
        }
        foreach (TitleInputBinding binding in bindings)
        {
            int index = profile.Bindings.FindIndex(b => string.Equals(b.Action, binding.Default.Action, StringComparison.OrdinalIgnoreCase));
            if (index < 0) { profile.Bindings.Add(binding.Default); }
            else { profile.Bindings[index] = binding.Default; }
        }
    }

    /// <summary>Whether the profile has any row for an action that can be pressed: a key, a mouse button, or a wheel notch.</summary>
    private static bool HasUsableRow(PlayerProfile? profile, string action)
    {
        IReadOnlyList<InputBinding> bindings = profile?.Bindings ?? PlayerProfile.DefaultBindings;
        foreach (InputBinding binding in bindings)
        {
            if (!string.Equals(binding.Action, action, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (string.Equals(binding.Device, InputBindingKeyNames.KeyboardDevice, StringComparison.OrdinalIgnoreCase)
                && InputBindingKeyNames.TryParseKeyboardKey(binding.Key, out _)) { return true; }
            if (string.Equals(binding.Device, InputBindingKeyNames.MouseDevice, StringComparison.OrdinalIgnoreCase)
                && (InputBindingKeyNames.TryParseMouseButton(binding.Key, out _)
                    || InputBindingKeyNames.TryParseMouseWheel(binding.Key, out _))) { return true; }
        }

        return false;
    }

    /// <summary>Samples held controls or frame edges, including user rebindings and explicitly unbound controls.</summary>
    /// <param name="profile">The active profile.</param>
    /// <param name="input">The current input frame.</param>
    /// <param name="bindings">The title's additional controls.</param>
    /// <param name="pressed">Whether to read rising edges rather than held keys.</param>
    /// <returns>The title's action mask.</returns>
    /// <remarks>
    /// The touch screen holds a title action's default key when the saved row cannot be pressed (see
    /// <c>GameHost.Want</c>), so this reads that same default when the profile has no usable row for
    /// the action: the two paths must agree, or the button holds a key the player never sees.
    /// A row the player rebound stays theirs alone — the default is only read when no row parses.
    /// </remarks>
    public static ulong Sample(PlayerProfile? profile, InputState input, IReadOnlyList<TitleInputBinding> bindings, bool pressed)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(bindings);
        ulong mask = 0;
        foreach (TitleInputBinding binding in bindings)
        {
            if (HasUsableRow(profile, binding.Default.Action))
            {
                if (pressed ? PlayerProfileInputBindings.WasPressed(profile, input, binding.Default.Action)
                    : PlayerProfileInputBindings.IsDown(profile, input, binding.Default.Action)) { mask |= binding.Mask; }
            }
            else if (string.Equals(binding.Default.Device, InputBindingKeyNames.KeyboardDevice, StringComparison.OrdinalIgnoreCase)
                && InputBindingKeyNames.TryParseKeyboardKey(binding.Default.Key, out KeyCode key)
                && (pressed ? input.Keyboard.WasPressed(key) : input.Keyboard.IsDown(key)))
            {
                mask |= binding.Mask;
            }
            else if (string.Equals(binding.Default.Device, InputBindingKeyNames.MouseDevice, StringComparison.OrdinalIgnoreCase)
                && InputBindingKeyNames.TryParseMouseButton(binding.Default.Key, out MouseButton button)
                && (pressed ? input.Mouse.WasPressed(button) : input.Mouse.IsDown(button)))
            {
                mask |= binding.Mask;
            }
        }
        return mask;
    }
}
