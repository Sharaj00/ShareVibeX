using Avalonia.Input;

namespace ShareVibeX.App.Editing;

public static class EditorKeyboard
{
    public static bool HandleHistory(CaptureEditorState state,Key key,KeyModifiers modifiers)
    {
        if(key!=Key.Z || !modifiers.HasFlag(KeyModifiers.Control))return false;
        if(modifiers.HasFlag(KeyModifiers.Shift))state.Document.Redo();else state.Document.Undo();
        state.SelectedId=null;return true;
    }
}
