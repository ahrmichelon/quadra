# Security

Quadra uses the macOS Accessibility permission to move other apps' windows. It reads only the
position, size and title of the focused window.

Its only network access is a request to GitHub, at most once a day, asking for the latest
release (`api.github.com/repos/vulpini-br/quadra/releases/latest`). Nothing about you or your
Mac is sent. You can turn it off in Settings. Quadra for Windows does the same with the list of
releases (`api.github.com/repos/vulpini-br/quadra/releases`), where it looks for the newest
Windows release, and needs no special permission to move windows.

Releases are signed with the project's own certificate ("Quadra Open Source"), so macOS keeps
the Accessibility permission across updates. They are not notarized by Apple yet.

If you find a security problem, please report it privately: open the **Security** tab of this
repository and click **Report a vulnerability**. Please don't open a public issue for it.

Fixes go into the latest version only.
