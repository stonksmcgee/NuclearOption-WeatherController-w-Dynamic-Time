# Nuclear Option - Weather Controller

Real-time weather control for Nuclear Option. Adjust time of day, cloud conditions, wind, and moon phase, or let the dynamic weather system generate realistic weather patterns automatically.

![BepInEx](https://img.shields.io/badge/BepInEx-5.x-blue) ![Game](https://img.shields.io/badge/Nuclear%20Option-Steam-black)

## Requirements

- [Nuclear Option](https://store.steampowered.com/app/2296550/Nuclear_Option/) on Steam
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) (Unity Mono)

## Installation

1. Install BepInEx 5.x into your Nuclear Option game folder
2. Run the game once to generate the BepInEx folder structure
3. Copy `WeatherController.dll` into `[Game Folder]\BepInEx\plugins\`
4. Launch the game

## Controls

| Key | Function |
|-----|----------|
| `F6` | Toggle weather control UI |

## Features

### Manual Weather Control
- **Time of Day** - 0-24h with quick presets (Dawn, Noon, Dusk, Night)
- **Sky Conditions** - Clear, Scattered, Broken, Overcast, Storm
- **Cloud Height** - 200m to 8000m
- **Wind** - Speed (0-30 m/s), Turbulence (0-100%), Direction (compass)
- **Moon Phase** - New Moon through Full Moon cycle

### Dynamic Weather System
- Automatic realistic weather pattern generation
- 4 intensity levels: Calm, Moderate, Active, Extreme
- Adjustable simulation speed (0.2x - 5x)
- Natural weather trends (improving / stable / worsening)

### Weather Presets
| Preset | Description |
|--------|-------------|
| Clear Day | Noon, clear skies, calm winds |
| Overcast | Afternoon, scattered clouds, moderate winds |
| Stormy | Afternoon, storm conditions, high winds |
| Dawn | Morning, clear skies, light winds |
| Dusk | Evening, clear skies, light winds |
| Night | Midnight, clear skies, half moon |

## Notes

- All changes apply in real-time during missions
- Dynamic weather mode disables manual sky/wind controls while active
- Time of day can always be changed manually even during dynamic weather
