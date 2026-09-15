# Triggered instructions

Keep default context light. Read external instructions only when the user's current message contains a trigger.

- `use-unity.bug-investigation` → read `/Users/yassine/Documents/Codex-User-Skills/unity/unity.bug-investigation.md`
- `use-react-hex` → read `/Users/yassine/Documents/Codex-User-Skills/react-hex.md`

In addition, `use-<folder-name>` means read every file in `/Users/yassine/Documents/Codex-User-Rules/<folder-name>/` (including files in its subfolders) completely before acting, then follow those instructions for that turn. This convention applies to any folder name, such as `use-unity` or `use-adventureGate`; it is not limited to a predefined list.

When triggered, read all mapped files and any applicable rule-folder files before acting and follow them for that turn. Without a trigger, do not read them. If a mapped file or rule folder is missing or unreadable, say so briefly instead of pretending it was loaded.
