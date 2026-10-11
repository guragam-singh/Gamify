GAMIFY — final responsive website prototype

FILES
- index.html
- fantasy-valley-animated.gif (animated version of the supplied fantasy valley scene)
- road-rush-poster.png (the supplied Road Rush poster, preserved exactly)
- spellbound-woods-poster.png (original poster artwork)
- cosmic-archive-poster.png (original poster artwork)

RUN
1. Extract the ZIP.
2. Keep all files together in the same folder.
3. Open index.html in a modern browser.

PAGE STRUCTURE
- Homepage: full-screen animated background, fixed navbar, letter-by-letter headline and Get Started.
- Dashboard: exactly three stacked sections: Take a Test, Learn, Leaderboard. No upload area or game library appears on the dashboard.
- Take a Test page: PDF upload section first, then exactly three game cards in a responsive grid.
- Learn page: three sample learning modules with working detail previews.
- Leaderboard page: rankings with distinct top-three styling and explicit demo-data label.
- Sign Up: registration modal with name, email, and password fields. It validates locally but does not create a real account.

FUNCTIONALITY AND LIMITATIONS
- PDF file picker, drag/drop, multiple file list, size display, remove controls, duplicate checks, PDF validation, and 20 MB per-file validation work locally.
- Generate Test opens a clearly labelled sample quiz because AI generation is not connected.
- Road Rush opens a clearly labelled Unity placeholder. Replace the handler in index.html with your Unity WebGL build or hosted URL when available.
- The other two games are locked and display Coming Soon.
- Learn module previews work.
- Navigation is a lightweight client-side interface and the navbar stays fixed.
- No files are uploaded to a server; no authentication, AI backend, persistent accounts, or live leaderboard is implemented.
