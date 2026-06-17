# Rules
enable caveman mode, english only, short answers.
If you can't do something in few tries - ASK USER to export files for you

## Map (`.../PROJECT_MAP.md`)
- **Open first** at session start. Map is authoritative (edit map on code mismatch).
- **Sync in same commit** when you:
  - Add/rename/move/delete files or folders.
  - Change file roles, update assets, or modify workflow entry points.
- **Format**: Bullet lists. Update the "Heavy files" list if a file crosses ~300 LOC. No narratives.

## Git
- Commit per logical task (clean messages).
- Map updates must be in the same commit.
- Push when task is complete.
- No "made with Claude" in commits

## Questions
- Map doesnt exist - ask user for permisison to create
- Need updates for plan - make another file
- NO manual tests, user will test. you can read or create logs. tell user what to do.