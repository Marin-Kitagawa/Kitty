# Kitty — Change Log

## [Unreleased]

### Added
- Comprehensive README.adoc with architecture diagrams, auth flow, UI anatomy, world palettes, data model, build/release docs
- D2 diagrams for system context, refresh pipeline, OAuth device flow, UI anatomy, world palettes, data model, auth state machine, release pipeline
- GitHub Actions release workflow (win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64)
- Clean transparent logo (kitty_logo-clean.png) with regenerated Assets/kitty.png (512×512) and Assets/kitty.ico
- Title bar logo replaces "K" placeholder; centered minimize glyph; tooltip foreground fixed
- Custom window chrome with drag/move/resize; CapButton caption buttons (46×44)
- Six visual worlds (Riso, Shelf, Board, Tape, Programme, Canon) with semantic token system
- OAuth 2.0 Device Flow authentication with token refresh
- Dual data sources: GitHub Search API + Trending HTML scrape
- Language filtering with live fetch + 24h cache
- Settings persistence (world, language, theme, source mode, token) to JSON
- Keyboard shortcuts (F5, Ctrl+W, Ctrl+G, Ctrl+F, Escape)
- Cross-platform single-file self-contained publishing

### Fixed
- Tooltip foreground (global TextBlock style overridden InkBrush → GroundABrush via ToolTip TextBlock style + explicit ToolTip.Tip TextBlocks)
- Minimize glyph alignment (replaced Path with centered Border)
- Title bar logo rendering (30×30 Image with HighQuality interpolation)
- Window caption button hit-testing (contiguous 46×44 CapButtons)

### Changed
- Logo updated from paper-background 2048² to transparent 500×500 clean version
- Assets/kitty.png regenerated to 512×512; Assets/kitty.ico to 7 PNG entries (16–256)
- ApplicationIcon in csproj now points to clean logo ICO

## [0.1.0] - 2026-10-22

### Added
- Initial Avalonia 12.1 / .NET 10 project structure
- MainViewModel with RepoRowViewModel, filter, source toggle, refresh cycle
- GitHubService with Search API and Trending scrape implementations
- GitHubAuth Device Flow implementation
- SettingsService JSON persistence
- Six-world theme system with ResourceDictionary swap
- Custom window chrome (WindowDecoration=None) with BeginMoveDrag/BeginResizeDrag
- Settings overlay with auth, theme, world, language cache controls