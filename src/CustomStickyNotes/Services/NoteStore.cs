using System.Collections.Generic;
using CustomStickyNotes.Models;

namespace CustomStickyNotes.Services;

public class NoteStore
{
    public List<NoteModel> Notes { get; private set; } = new();

    public void Load()
    {
        Notes = JsonStore.Load<List<NoteModel>>(AppPaths.NotesFile) ?? new List<NoteModel>();
    }

    public void Save()
    {
        JsonStore.Save(AppPaths.NotesFile, Notes);
    }
}
