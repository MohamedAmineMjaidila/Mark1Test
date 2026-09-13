# Triggered instructions

Keep default context light. Read an external instruction file only when the user's current message contains its exact trigger:

- `use-unity.bug-investigation` → read `/Users/yassine/Documents/Codex-User-Skills/unity/unity.bug-investigation.md`
- `use-react-hex` → read `/Users/yassine/Documents/Codex-User-Skills/react-hex.md`

When triggered, read the mapped file completely before acting and follow it for that turn. If several triggers appear, read all mapped files. Without a trigger, do not read them. If a mapped file is missing or unreadable, say so briefly instead of pretending it was loaded.
