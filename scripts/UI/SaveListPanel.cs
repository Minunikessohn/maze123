#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Save;

namespace Maze.UI;

public partial class SaveListPanel : VBoxContainer
{
    private ItemList _saveList = null!;
    private Label _emptyLabel = null!;
    private Label _selectionLabel = null!;

    public event Action? SelectionChanged;

    public string? SelectedSaveId
    {
        get
        {
            int[] selection = _saveList.GetSelectedItems();
            if (selection.Length == 0)
            {
                return null;
            }

            int index = selection[0];
            Variant metadata = _saveList.GetItemMetadata(index);
            return metadata.VariantType == Variant.Type.Nil ? _saveList.GetItemText(index) : metadata.AsString();
        }
    }

    public string? SelectedSaveLabel
    {
        get
        {
            int[] selection = _saveList.GetSelectedItems();
            return selection.Length == 0 ? null : _saveList.GetItemText(selection[0]);
        }
    }

    public override void _Ready()
    {
        _saveList = GetNode<ItemList>("SaveList");
        _emptyLabel = GetNode<Label>("EmptyLabel");
        _selectionLabel = GetNode<Label>("SelectionLabel");

        _saveList.ItemSelected += _ => OnSelectionChanged();
        UpdateVisualState();
    }

    public void SetSaveSlots(IEnumerable<SaveSlotSummary> saveSlots)
    {
        _saveList.Clear();

        foreach (SaveSlotSummary saveSlot in saveSlots)
        {
            int index = _saveList.ItemCount;
            _saveList.AddItem(saveSlot.ToDisplayLabel());
            _saveList.SetItemMetadata(index, saveSlot.SaveId);
        }

        if (_saveList.ItemCount > 0)
        {
            _saveList.Select(0);
        }

        UpdateVisualState();
        SelectionChanged?.Invoke();
    }

    private void OnSelectionChanged()
    {
        UpdateVisualState();
        SelectionChanged?.Invoke();
    }

    private void UpdateVisualState()
    {
        bool hasItems = _saveList.ItemCount > 0;
        _emptyLabel.Visible = !hasItems;
        _selectionLabel.Text = hasItems && SelectedSaveLabel is string saveLabel
            ? $"Ausgewaehlt: {saveLabel}"
            : "Kein Save ausgewaehlt.";
    }
}