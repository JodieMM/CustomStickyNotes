# Sticky Notes App — Spec

## Overview
A Windows desktop sticky-notes app, inspired by the built-in Microsoft Sticky Notes app, with a grid-based layout, custom color palette, smarter position memory, and light note-organization tools. Built in **C# / WPF**.

## Goals
- Keep the core sticky-note experience (quick notes, rich text, always visible/available) but fix the pain points of the original: notes drift into disorganized free-floating chaos, appearance can't be customized, and there's no way to manage/find notes once there are a lot of them.

## Core Features

### 1. Grid-snap placement
- Notes snap to an invisible grid when dragged/dropped, instead of free pixel placement.
- Grid cell size should be configurable (default suggestion: matches the default note size, e.g. notes snap edge-to-edge with a small gutter).
- Snapping applies per-monitor (each screen has its own grid).

### 2. Custom color palette
- Replace the default Windows Sticky Notes colors with a curated palette that's easy to swap/extend later. Suggested starting palette (soft/pastel aesthetic):
  - Blush pink `#F7D6E0`
  - Lavender `#E3D9F7`
  - Mint `#D6F0E0`
  - Peach `#FBE3D0`
  - Sky blue `#D6EAF7`
  - Cream `#FFF6E5`
- Color picker available per-note; palette itself should be easy to edit in a config/theme file rather than hardcoded, so it can be adjusted later.

### 3. Position memory with monitor-awareness
- Each note remembers its last grid position (monitor index + grid coordinates) and restores there on launch.
- If the number of connected monitors has changed since last save (e.g. laptop undocked, external monitor unplugged):
  - Treat any note whose saved monitor no longer exists as having a **temporary** position — place it on the primary monitor (e.g. top-left open grid slot) rather than losing it or forcing it off-screen.
  - When the original monitor reappears, restore the note back to its saved position on that monitor.
- Store monitor identity in a way that's stable across reconnects (not just index order, since that can shift) — e.g. by resolution + relative position, or a saved device ID if available.

### 4. Note appearance & resizing
- Notes should be resizable (drag corner/edge), unlike the fairly rigid sizing in the original.
- Appearance customization beyond color: at minimum, allow adjusting note size defaults and maybe font.

### 5. Organization / findability
- Since free-placement frustration is being solved by grid-snap, also add a lightweight way to manage notes once there are many:
  - A list/overview view (e.g. a small sidebar or a "show all notes" panel) showing all notes as thumbnails or titles, so the user can jump to / bring a note to front without hunting across monitors.
  - Basic search across note text.
  - Optional: an "archive" or "close" action that hides a note from the desktop but keeps it retrievable from the list view, rather than only delete-or-keep-forever.

### 6. Text formatting (parity with original)
- Rich text support matching the original Sticky Notes: bold, italic, strikethrough, bullet lists, numbered lists.
- Use a standard WPF RichTextBox (or similar) rather than plain text.

## Technical Notes

- **Language/Framework:** C#, WPF (.NET)
- **Persistence:** Local file-based storage (JSON or a lightweight embedded DB like SQLite) storing: note content (as RTF/XAML fragment or similar to preserve formatting), color, size, grid position, monitor identity, archived/active state.
- **Window behavior:**
  - Each note is a separate borderless, always-on-top-optional WPF window.
  - System tray icon for: create new note, open list/overview view, exit app, toggle "notes visible" globally.
  - Consider a lightweight "launch on startup" option (Windows startup registry entry or shortcut).
- **Multi-monitor handling:** Use `System.Windows.Forms.Screen.AllScreens` (or WPF equivalent) to enumerate monitors, detect changes on launch and on display-settings-changed events, and re-map notes per the position memory rules above.

## Nice-to-Haves (stretch, not required for v1)
- Tagging or category labels on notes (separate from color).
- Reminders/due dates attached to a note.
- Keyboard shortcut to spawn a new note globally.
