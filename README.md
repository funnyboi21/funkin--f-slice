# 🎵 Funkin' F-Slice

A **Friday Night Funkin'** fan engine built from the ground up in **Godot 4**, using a hybrid **GDScript + C#** architecture for flexibility and performance.

---

## About

Funkin' F-Slice is an open-source FNF engine reimagined in Godot 4. It aims to provide a clean, moddable foundation for creating Friday Night Funkin' experiences with the power of Godot's modern rendering and tooling.

---

## Features

- Built on **Godot 4** with a hybrid GDScript / C# codebase
- Modular engine structure (`engine/`, `source/`, `assets/`, `addons/`)
- Custom animated hardware cursor support
- MIT licensed — free to use, fork, and modify

---

## Getting Started

### Prerequisites

- [Godot 4](https://godotengine.org/download) (with .NET / Mono support for C#)
- [.NET SDK 6.0+](https://dotnet.microsoft.com/download)

### Running the Project

1. Clone the repository:
   ```bash
   git clone https://github.com/funnyboi21/funkin--f-slice.git
   ```
2. Open **Godot 4** and import the project by selecting `project.godot`.
3. Hit **Run** (F5) to launch.

> Make sure you're using the **.NET version** of Godot 4, not the standard build — C# scripts won't compile otherwise.

---

## Project Structure

```
funkin--f-slice/
├── addons/       # Godot plugins and third-party addons
├── assets/       # Sprites, music, sounds, fonts
├── engine/       # Core engine logic
├── source/       # Game-specific scripts (GDScript & C#)
├── project.godot # Godot project configuration
└── icon.svg      # Project icon
```

---

## Language Breakdown

| Language  | Usage |
|-----------|-------|
| GDScript  | ~65%  |
| C#        | ~35%  |

GDScript is used for gameplay logic and scene scripting. C# handles performance-critical systems.

---

## Contributing

Pull requests are welcome! If you have a bug fix, feature idea, or improvement:

1. Fork the repo
2. Create a branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Open a Pull Request

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## Credits

- Original game: [Friday Night Funkin'](https://github.com/FunkinCrew/Funkin) by The Funkin' Crew
- Engine: [Godot 4](https://godotengine.org/)
