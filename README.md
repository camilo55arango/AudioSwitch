# AudioSwitch

App de bandeja para Windows para cambiar entre audífonos y silenciar el audio o el micrófono con atajos de teclado globales, viendo siempre qué está activo.

Pensada para quien tiene varios audífonos conectados a la vez (por ejemplo uno por USB-C y otro por el jack de 3.5 mm) y quiere pasar de uno a otro sin abrir la configuración de sonido.

## Funciones

- **Perfiles**: cada perfil guarda una salida y un micrófono. Con un atajo cambias los dos a la vez.
- **Silenciar** el micrófono o el audio con un atajo global, que funciona aunque la app no tenga el foco.
- **Icono en la bandeja**:
  - verde si el micrófono está activo y rojo si está silenciado,
  - punto naranja si el audio está silenciado,
  - clic izquierdo para silenciar el micrófono y clic derecho para el menú.
- **Widget flotante**: siempre visible, muestra la salida y el micrófono con su estado (ON/OFF). Haz clic en una fila para silenciarla y arrástralo para moverlo.
- **Aviso en pantalla** cada vez que cambia algo, también si el cambio viene de Windows o desconectas un dispositivo.
- **Atajos configurables** desde una ventana: haz clic y presiona la combinación.
- Cambia el predeterminado de **Comunicaciones** junto con el normal, para que Teams, Zoom y Discord lo sigan.
- **Iniciar con Windows** (opcional).

## Atajos por defecto

| Atajo | Acción |
|---|---|
| `Ctrl+Alt+M` | Silenciar / activar micrófono |
| `Ctrl+Alt+A` | Silenciar / activar audio |
| `Ctrl+Alt+S` | Alternar entre perfil 1 y 2 |
| `Ctrl+Alt+1` / `Ctrl+Alt+2` | Ir a un perfil |
| `Ctrl+Alt+W` | Mostrar / ocultar widget |

Se cambian desde el menú de la bandeja, en **Configurar atajos…**. Inicio, Fin, Re Pág, Av Pág, Insert, Pausa y F1–F24 se pueden usar solas.

## Requisitos

- Windows 10 u 11.
- .NET Framework 4.x, que ya viene instalado en Windows. No hace falta instalar el .NET SDK.

## Compilar

```powershell
.\build.ps1
```

El script usa el compilador de C# que trae Windows (`csc.exe`) y genera `bin\AudioSwitch.exe`.

## Uso

1. Ejecuta `bin\AudioSwitch.exe`. Aparece un icono en la bandeja del sistema.
2. En el primer arranque se crean hasta dos perfiles automáticamente, uno por cada adaptador que tenga salida y micrófono.
3. Si algún perfil no quedó bien, elige la salida y el micrófono desde el menú y usa **Guardar configuración actual como → Perfil 1/2**.

La configuración se guarda en `%AppData%\AudioSwitch\settings.ini`.

## Limitaciones

- **Apps con dispositivo fijo**: Discord, Teams, Zoom y similares tienen que estar configuradas en "Predeterminado" para seguir el cambio.
- **Mute interno de esas apps**: el silencio se aplica a nivel de Windows, así que el botón de mute de Teams o Discord no cambia de estado.
- **Botón físico de mute del cable**: muchas veces no se comunica a Windows, así que la app no lo detecta.
- **Jack de 3.5 mm**: depende del driver de audio. Si este no separa el jack de los parlantes internos, no se puede distinguir.
- **Juegos en pantalla completa exclusiva**: el aviso en pantalla no se ve, pero los atajos siguen funcionando.
- **API no documentada**: el cambio de dispositivo predeterminado usa la interfaz `IPolicyConfig`. Es la misma que usan herramientas como SoundSwitch y EarTrumpet.

## Estructura

```
src/
  Program.cs        Punto de entrada (instancia única)
  TrayApp.cs        Lógica principal: estado, menú, perfiles y atajos
  AudioService.cs   Dispositivos, predeterminado y silencio
  CoreAudio.cs      Interop con Windows Core Audio e IPolicyConfig
  Hotkeys.cs        Registro de atajos globales
  HotkeyDialog.cs   Ventana para configurar atajos
  OsdForm.cs        Aviso en pantalla
  WidgetForm.cs     Widget flotante
  Ui.cs             Colores, iconos y escalado DPI
  Settings.cs       Lectura y escritura de settings.ini
app.manifest        Manifiesto (DPI)
build.ps1           Script de compilación
```
