# T-062 — Publish Liberty+

Status: COMPLETE — repository publication only; feature development stays paused.

Owner requested creation of the new GitHub repository and publication of both
separated repos. Liberty+ is https://github.com/im576/liberty-plus, with public
visibility matching https://github.com/im576/liberty-framework.

Publish main and the four archive/legacy-* candidate snapshots using ordinary
pushes. Archive candidates remain unaccepted and are not merged into main.
Ignored build outputs, validation workspaces and game files stay local. Preserve
framework.lock.json: publication changes documentation, not the tested source
contract or pinned framework revision. No installation or game test is requested.

Validation: git diff --check; verify GitHub main equals local HEAD in both repos,
and verify archived refs. Existing T-061 build/test evidence remains applicable.
