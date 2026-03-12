using Godot;
using System;

public partial class PopupInputHelper : Node
{
    public Node Target { get; set; }

    // simple helper that forwards any key or click to the parent popup
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Echo) return;
        if (Target != null && @event.IsPressed() && (@event is InputEventKey || @event is InputEventMouseButton))
        {
            // notify the target controller
            Target.Call("OnNextSlide");
            GetViewport().SetInputAsHandled();
        }
    }
}
