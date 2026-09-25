using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MrGlim.HorseTack.Framework;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Characters;
using StardewValley.Menus;

namespace MrGlim.HorseTack.Menus
{
    /// <summary>The stable wizard: horse -> coat -> saddle -> pad -> bridle -> styling -> confirm, with a live animated preview.</summary>
    internal sealed class TackWizardMenu : IClickableMenu
    {
        /*********
        ** Types / constants
        *********/
        private enum Step { Horse, Coat, Saddle, Pad, Bridle, Style, Confirm }

        private sealed record Choice(string Id, string Label, string Tag = "");

        private const int RowHeight = 56;
        private int VisibleRows = 8;
        private int PreviewScale = 9;
        private const int FrameSize = 32;

        private const int RowIdBase = 1000;
        private const int UpArrowId = 900;
        private const int DownArrowId = 901;
        private const int BackId = 800;
        private const int CancelId = 801;
        private const int NextId = 802;
        private const int FilterLeftId = 902;
        private const int FilterRightId = 903;

        /*********
        ** State
        *********/
        private readonly AssetRegistry Registry;
        private readonly TextureManager Textures;
        private readonly TackService Service;
        private readonly List<Horse> Horses;

        private Horse CurrentHorse;
        private TackSelection Selection;
        private List<Step> Steps = new();
        private int StepIndex;
        private List<Choice> Choices = new();
        private int SelectedIndex;
        private int ScrollOffset;
        /// <summary>Selected collection filter per layer (0 = all).</summary>
        private readonly Dictionary<TackLayer, int> Filters = new();
        /// <summary>Filter names for the current step: "All" followed by each collection that has art.</summary>
        private List<string> FilterNames = new();

        /*********
        ** UI components (fields are picked up by populateClickableComponentList)
        *********/
        public List<ClickableComponent> Rows = new();
        public ClickableTextureComponent UpArrow = null!;
        public ClickableTextureComponent DownArrow = null!;
        public ClickableComponent BackButton = null!;
        public ClickableComponent CancelButton = null!;
        public ClickableComponent NextButton = null!;
        public ClickableTextureComponent FilterLeft = null!;
        public ClickableTextureComponent FilterRight = null!;

        private Rectangle PreviewBox;
        private Rectangle ListBox;
        private Rectangle FilterBar;

        /// <summary>Whether the current step shows a collection filter (only when its art spans two or more collections).</summary>
        private bool HasFilter => this.CurrentStep != Step.Confirm && LayerFor(this.CurrentStep) != null && this.FilterNames.Count > 2;

        private Step CurrentStep => this.Steps[this.StepIndex];

        /*********
        ** Public
        *********/
        public TackWizardMenu(AssetRegistry registry, TextureManager textures, TackService service, List<Horse> horses, Horse initialHorse)
        {
            this.Registry = registry;
            this.Textures = textures;
            this.Service = service;
            this.Horses = horses;
            this.CurrentHorse = initialHorse;
            this.Selection = this.Registry.Canonicalize(TackSelection.FromModData(initialHorse).Clone());

            this.Layout();
            this.RebuildSteps(keep: null);
            this.StepIndex = 0;
            this.LoadStep();

            if (Game1.options.SnappyMenus)
                this.snapToDefaultClickableComponent();
        }

        public override void snapToDefaultClickableComponent()
        {
            if (this.CurrentStep != Step.Confirm && this.Choices.Count > 0)
            {
                int row = Math.Clamp(this.SelectedIndex - this.ScrollOffset, 0, VisibleRows - 1);
                this.currentlySnappedComponent = this.getComponentWithID(RowIdBase + row);
            }
            else
                this.currentlySnappedComponent = this.getComponentWithID(NextId);
            this.snapCursorToCurrentSnappedComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.Layout();
            this.UpdateNeighbors();
        }

        /*********
        ** Input
        *********/
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            if (Game1.activeClickableMenu != this)
                return; // closed by the X button

            if (this.BackButton.containsPoint(x, y))
                this.GoBack();
            else if (this.CancelButton.containsPoint(x, y))
                this.Cancel();
            else if (this.NextButton.containsPoint(x, y))
                this.GoNext();
            else if (this.FilterLeft.visible && this.FilterLeft.containsPoint(x, y))
                this.ChangeFilter(-1);
            else if (this.FilterRight.visible && this.FilterRight.containsPoint(x, y))
                this.ChangeFilter(1);
            else if (this.UpArrow.visible && this.UpArrow.containsPoint(x, y))
                this.Scroll(-1, playSound: true);
            else if (this.DownArrow.visible && this.DownArrow.containsPoint(x, y))
                this.Scroll(1, playSound: true);
            else if (this.CurrentStep != Step.Confirm)
            {
                for (int i = 0; i < this.Rows.Count; i++)
                {
                    if (this.Rows[i].visible && this.Rows[i].containsPoint(x, y))
                    {
                        this.Select(this.ScrollOffset + i);
                        break;
                    }
                }
            }
        }

        public override void receiveGamePadButton(Buttons b)
        {
            base.receiveGamePadButton(b);
            switch (b)
            {
                case Buttons.LeftShoulder:
                    this.Page(-1);
                    break;
                case Buttons.RightShoulder:
                    this.Page(1);
                    break;
                case Buttons.LeftTrigger:
                    this.GoBack();
                    break;
                case Buttons.RightTrigger:
                    this.GoNext();
                    break;
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            bool gamepadSnapping = Game1.options.SnappyMenus && Game1.options.gamepadControls;
            if (!gamepadSnapping && this.CurrentStep != Step.Confirm)
            {
                if (key == Keys.Up) { this.Select(this.SelectedIndex - 1); return; }
                if (key == Keys.Down) { this.Select(this.SelectedIndex + 1); return; }
                if (key == Keys.Left) { this.ChangeFilter(-1); return; }
                if (key == Keys.Right) { this.ChangeFilter(1); return; }
            }
            if (this.CurrentStep != Step.Confirm)
            {
                if (key == Keys.PageUp) { this.Page(-1); return; }
                if (key == Keys.PageDown) { this.Page(1); return; }
            }
            if (!gamepadSnapping)
            {
                if (key == Keys.Enter) { this.GoNext(); return; }
                if (key == Keys.Back) { this.GoBack(); return; }
            }
            base.receiveKeyPress(key); // Escape / menu button closes, gamepad snapping
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            this.Scroll(direction > 0 ? -3 : 3, playSound: true); // quick scroll: three rows per notch
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.UpArrow.tryHover(x, y);
            this.DownArrow.tryHover(x, y);
            this.FilterLeft.tryHover(x, y);
            this.FilterRight.tryHover(x, y);
        }

        /*********
        ** Draw
        *********/
        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            // title
            SpriteText.drawStringWithScrollCenteredAt(b, I18n.Get("wizard.title"), this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);

            this.DrawPreview(b);

            // step heading
            int headX = this.ListBox.X;
            int headY = this.PreviewBox.Y - 8;
            string heading = I18n.Get("wizard.step", new { current = this.StepIndex + 1, total = this.Steps.Count, name = this.StepName(this.CurrentStep) });
            Utility.drawTextWithShadow(b, heading, Game1.dialogueFont, new Vector2(headX, headY), Game1.textColor);

            if (this.CurrentStep == Step.Confirm)
                this.DrawSummary(b);
            else
                this.DrawList(b);

            this.DrawButton(b, this.BackButton, I18n.Get("button.back"), this.StepIndex > 0);
            this.DrawButton(b, this.CancelButton, I18n.Get("button.cancel"), true);
            this.DrawButton(b, this.NextButton, this.CurrentStep == Step.Confirm ? I18n.Get("button.apply") : I18n.Get("button.next"), true);

            base.draw(b); // close button
            this.drawMouse(b);
        }

        private void DrawPreview(SpriteBatch b)
        {
            Rectangle box = this.PreviewBox;
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), box.X, box.Y, box.Width, box.Height, Color.White, 4f, false);
            Rectangle inner = new(box.X + 16, box.Y + 16, box.Width - 32, box.Height - 32);
            b.Draw(Game1.staminaRect, inner, new Color(118, 166, 88));

            Texture2D? tex = this.Textures.GetPreview(this.CurrentHorse, this.Selection);
            if (tex != null && tex.Width >= FrameSize && tex.Height >= FrameSize * 3)
            {
                double ms = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
                int phase = (int)(ms / 2400 % 4); // right, down, left, up
                int frame = (int)(ms / 110 % 7);
                int row = phase switch { 0 => 1, 1 => 0, 2 => 1, _ => 2 };
                bool flip = phase == 2;
                int framesPerRow = Math.Max(1, tex.Width / FrameSize);
                Rectangle src = new((frame % framesPerRow) * FrameSize, row * FrameSize, FrameSize, FrameSize);
                int size = FrameSize * PreviewScale;
                Vector2 pos = new(inner.Center.X - size / 2, inner.Center.Y - size / 2);
                // shadow
                b.Draw(Game1.shadowTexture, new Vector2(inner.Center.X, pos.Y + size - 24), Game1.shadowTexture.Bounds, Color.White * 0.6f, 0f, new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 6f, SpriteEffects.None, 0.9f);
                b.Draw(tex, pos, src, Color.White, 0f, Vector2.Zero, PreviewScale, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.91f);
            }

            // horse name + owner under the preview
            string name = HorseUtil.Name(this.CurrentHorse);
            Vector2 nameSize = Game1.smallFont.MeasureString(name);
            Utility.drawTextWithShadow(b, name, Game1.smallFont, new Vector2(box.Center.X - nameSize.X / 2, box.Bottom + 8), Game1.textColor);
            string owner = I18n.Get("wizard.owner", new { owner = HorseUtil.OwnerName(this.CurrentHorse) });
            Vector2 ownerSize = Game1.smallFont.MeasureString(owner);
            Utility.drawTextWithShadow(b, owner, Game1.smallFont, new Vector2(box.Center.X - ownerSize.X / 2, box.Bottom + 8 + nameSize.Y), Game1.textColor * 0.8f);
        }

        private void DrawList(SpriteBatch b)
        {
            if (this.HasFilter)
            {
                this.FilterLeft.draw(b);
                this.FilterRight.draw(b);
                int filterIndex = this.CurrentFilterIndex();
                string name = filterIndex == 0 ? I18n.Get("filter.all") : this.FilterNames[filterIndex];
                string text = I18n.Get("filter.label", new { name, current = filterIndex + 1, total = this.FilterNames.Count });
                float maxFilterWidth = this.FilterRight.bounds.X - this.FilterLeft.bounds.Right - 16;
                while (text.Length > 4 && Game1.smallFont.MeasureString(text).X > maxFilterWidth)
                    text = text[..^2];
                Vector2 filterSize = Game1.smallFont.MeasureString(text);
                Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(this.FilterBar.Center.X - filterSize.X / 2, this.FilterBar.Center.Y - filterSize.Y / 2), Game1.textColor);
            }

            for (int i = 0; i < this.Rows.Count; i++)
            {
                ClickableComponent row = this.Rows[i];
                if (!row.visible)
                    continue;
                int index = this.ScrollOffset + i;
                Choice choice = this.Choices[index];
                bool selected = index == this.SelectedIndex;
                bool hovered = row.containsPoint(Game1.getMouseX(), Game1.getMouseY());
                if (selected)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.75f);
                else if (hovered)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.35f);

                int textX = row.bounds.X + 12;
                // small swatch: the option's own sprite (right-facing frame)
                Texture2D? swatch = this.CurrentStep == Step.Horse || choice.Id == "" ? null : this.Registry.GetTexture(choice.Id);
                if (swatch != null && swatch.Width >= FrameSize * 2 && swatch.Height >= FrameSize * 2)
                {
                    b.Draw(swatch, new Vector2(row.bounds.X + 4, row.bounds.Y + (RowHeight - 48) / 2 - 2), new Rectangle(FrameSize, FrameSize, FrameSize, FrameSize), Color.White, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0.9f);
                    textX = row.bounds.X + 60;
                }

                float tagWidth = 0;
                if (choice.Tag != "")
                {
                    Vector2 tagSize = Game1.smallFont.MeasureString(choice.Tag);
                    tagWidth = tagSize.X + 16;
                    Utility.drawTextWithShadow(b, choice.Tag, Game1.smallFont, new Vector2(row.bounds.Right - tagSize.X - 8, row.bounds.Y + (RowHeight - tagSize.Y) / 2), Game1.textColor * 0.55f);
                }

                string label = choice.Label;
                float maxWidth = row.bounds.Right - textX - 8 - tagWidth;
                while (label.Length > 4 && Game1.smallFont.MeasureString(label).X > maxWidth)
                    label = label[..^2];
                if (label != choice.Label)
                    label = label.TrimEnd() + "…";
                Vector2 size = Game1.smallFont.MeasureString(label);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(textX, row.bounds.Y + (RowHeight - size.Y) / 2), Game1.textColor);
            }

            if (this.UpArrow.visible)
                this.UpArrow.draw(b);
            if (this.DownArrow.visible)
                this.DownArrow.draw(b);

            // no art for this layer: explain where to add it
            if (LayerFor(this.CurrentStep) is TackLayer emptyLayer && this.Registry.Get(emptyLayer).Count == 0)
            {
                int lastRow = Math.Min(this.Choices.Count, this.Rows.Count) - 1;
                int noteY = (lastRow >= 0 ? this.Rows[lastRow].bounds.Bottom : this.ListBox.Y) + 16;
                string key = this.Registry.TotalCount == 0 ? "wizard.no-art" : "wizard.empty-layer";
                string note = Game1.parseText(I18n.Get(key, new { folder = TackLayers.FolderName(emptyLayer) }), Game1.smallFont, this.ListBox.Width - 24);
                Utility.drawTextWithShadow(b, note, Game1.smallFont, new Vector2(this.ListBox.X + 12, noteY), Game1.textColor * 0.8f);
            }

            // position indicator
            string pos = $"{(this.SelectedIndex >= 0 ? (this.SelectedIndex + 1).ToString() : "-")}/{this.Choices.Count}";
            Vector2 posSize = Game1.smallFont.MeasureString(pos);
            Utility.drawTextWithShadow(b, pos, Game1.smallFont, new Vector2(this.ListBox.Right - posSize.X, this.ListBox.Bottom + 4), Game1.textColor * 0.8f);
        }

        private void DrawSummary(SpriteBatch b)
        {
            int x = this.ListBox.X + 8;
            int y = this.FilterBar.Y + 8;
            var lines = new List<(string Label, string Value)>();
            if (this.Horses.Count > 1)
                lines.Add((this.StepName(Step.Horse), HorseUtil.Name(this.CurrentHorse)));
            lines.Add((this.StepName(Step.Coat), this.Registry.DisplayName(TackLayer.Coat, this.Selection.Coat)));
            lines.Add((this.StepName(Step.Saddle), this.Registry.DisplayName(TackLayer.Saddle, this.Selection.Saddle)));
            if (this.Selection.Saddle != "")
                lines.Add((this.StepName(Step.Pad), this.Registry.DisplayName(TackLayer.Pad, this.Selection.Pad)));
            lines.Add((this.StepName(Step.Bridle), this.Registry.DisplayName(TackLayer.Bridle, this.Selection.Bridle)));
            lines.Add((this.StepName(Step.Style), this.Registry.DisplayName(TackLayer.Style, this.Selection.Style)));

            foreach (var (label, value) in lines)
            {
                Utility.drawTextWithShadow(b, label + ":", Game1.smallFont, new Vector2(x, y), Game1.textColor * 0.8f);
                Utility.drawTextWithShadow(b, value, Game1.smallFont, new Vector2(x + 180, y), Game1.textColor);
                y += 48;
            }
            y += 16;
            string hint = Game1.parseText(I18n.Get("wizard.confirm-hint"), Game1.smallFont, this.ListBox.Width - 16);
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(x, y), Game1.textColor * 0.8f);
        }

        private void DrawButton(SpriteBatch b, ClickableComponent button, string label, bool enabled)
        {
            bool hovered = enabled && button.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), button.bounds.X, button.bounds.Y, button.bounds.Width, button.bounds.Height, hovered ? Color.Wheat : Color.White, 4f, false);
            Vector2 size = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(button.bounds.Center.X - size.X / 2, button.bounds.Center.Y - size.Y / 2 + 2), enabled ? Game1.textColor : Game1.textColor * 0.35f);
        }

        /*********
        ** Steps
        *********/
        private string StepName(Step step) => step switch
        {
            Step.Horse => I18n.Get("step.horse"),
            Step.Coat => I18n.Get("step.coat"),
            Step.Saddle => I18n.Get("step.saddle"),
            Step.Pad => I18n.Get("step.pad"),
            Step.Bridle => I18n.Get("step.bridle"),
            Step.Style => I18n.Get("step.style"),
            _ => I18n.Get("step.confirm")
        };

        private static TackLayer? LayerFor(Step step) => step switch
        {
            Step.Coat => TackLayer.Coat,
            Step.Saddle => TackLayer.Saddle,
            Step.Pad => TackLayer.Pad,
            Step.Bridle => TackLayer.Bridle,
            Step.Style => TackLayer.Style,
            _ => null
        };

        /// <summary>Recompute which steps apply. The pad step only appears when a saddle is chosen; overlay steps with no art are skipped (unless the horse already has a value there, so it can be removed). Coat always stays for Keep current.</summary>
        private void RebuildSteps(Step? keep)
        {
            var steps = new List<Step>();
            if (this.Horses.Count > 1)
                steps.Add(Step.Horse);
            steps.Add(Step.Coat);
            if (this.HasChoices(TackLayer.Saddle))
                steps.Add(Step.Saddle);
            if (this.Selection.Saddle != "" && this.HasChoices(TackLayer.Pad))
                steps.Add(Step.Pad);
            if (this.HasChoices(TackLayer.Bridle))
                steps.Add(Step.Bridle);
            if (this.HasChoices(TackLayer.Style))
                steps.Add(Step.Style);
            steps.Add(Step.Confirm);
            this.Steps = steps;

            if (keep != null)
            {
                int index = this.Steps.IndexOf(keep.Value);
                this.StepIndex = index >= 0 ? index : Math.Min(this.StepIndex, this.Steps.Count - 1);
            }
        }

        private bool HasChoices(TackLayer layer) => this.Registry.Get(layer).Count > 0 || this.Selection.Get(layer) != "";

        private int CurrentFilterIndex()
        {
            if (LayerFor(this.CurrentStep) is not TackLayer layer)
                return 0;
            int index = this.Filters.TryGetValue(layer, out int i) ? i : 0;
            return index >= 0 && index < this.FilterNames.Count ? index : 0;
        }

        /// <summary>Cycle the collection filter for the current step.</summary>
        private void ChangeFilter(int delta)
        {
            if (!this.HasFilter || LayerFor(this.CurrentStep) is not TackLayer layer)
                return;
            int count = this.FilterNames.Count;
            this.Filters[layer] = ((this.CurrentFilterIndex() + delta) % count + count) % count;
            Game1.playSound("shwip");
            this.LoadStep();
            if (Game1.options.SnappyMenus)
                this.snapToDefaultClickableComponent();
        }

        /// <summary>Jump a page of visible rows up or down.</summary>
        private void Page(int delta)
        {
            if (this.CurrentStep == Step.Confirm || this.Choices.Count == 0)
                return;
            int from = Math.Max(0, this.SelectedIndex);
            this.Select(from + delta * this.VisibleRows, snap: true);
        }

        /// <summary>Load the choices for the current step and select the current value.</summary>
        private void LoadStep()
        {
            Step step = this.CurrentStep;
            var choices = new List<Choice>();
            int selected = 0;
            this.FilterNames = new List<string>();

            if (step == Step.Horse)
            {
                foreach (Horse horse in this.Horses)
                {
                    string owner = HorseUtil.OwnerName(horse);
                    choices.Add(new Choice(horse.HorseId.ToString(), $"{HorseUtil.Name(horse)} ({owner})"));
                }
                selected = Math.Max(0, this.Horses.IndexOf(this.CurrentHorse));
            }
            else if (LayerFor(step) is TackLayer layer)
            {
                this.FilterNames.Add("");
                this.FilterNames.AddRange(this.Registry.Collections(layer));
                int filterIndex = this.CurrentFilterIndex();
                string? collection = this.FilterNames.Count > 2 && filterIndex > 0 ? this.FilterNames[filterIndex] : null;

                choices.Add(new Choice("", layer == TackLayer.Coat ? I18n.Get("option.keep") : I18n.Get("option.none")));
                foreach (TackOption option in this.Registry.Get(layer))
                {
                    if (collection == null)
                        choices.Add(new Choice(option.Id, option.DisplayName, option.Source));
                    else if (string.Equals(option.Collection, collection, StringComparison.OrdinalIgnoreCase))
                        choices.Add(new Choice(option.Id, option.ShortName, option.Source));
                }

                string current = this.Selection.Get(layer);
                selected = choices.FindIndex(c => string.Equals(c.Id, current, StringComparison.OrdinalIgnoreCase));
                if (selected < 0 && collection != null)
                    selected = -1; // the current value is in another collection: nothing highlighted
                else if (selected < 0)
                {
                    // keep an unknown stored value selectable rather than silently dropping it
                    choices.Add(new Choice(current, I18n.Get("option.missing", new { id = current })));
                    selected = choices.Count - 1;
                }
            }

            this.Choices = choices;
            this.SelectedIndex = selected;
            this.ScrollOffset = 0;
            this.EnsureVisible();
            this.UpdateRows();
        }

        private void Select(int index, bool snap = false)
        {
            if (this.CurrentStep == Step.Confirm || this.Choices.Count == 0)
                return;
            index = Math.Clamp(index, 0, this.Choices.Count - 1);
            if (index == this.SelectedIndex && !snap)
                return;

            this.SelectedIndex = index;
            Choice choice = this.Choices[index];
            Step step = this.CurrentStep;

            if (step == Step.Horse)
            {
                Horse horse = this.Horses.First(h => h.HorseId.ToString() == choice.Id);
                if (!ReferenceEquals(horse, this.CurrentHorse))
                {
                    this.CurrentHorse = horse;
                    this.Selection = this.Registry.Canonicalize(TackSelection.FromModData(horse).Clone());
                }
            }
            else if (LayerFor(step) is TackLayer layer)
            {
                this.Selection.Set(layer, choice.Id);
                this.Selection.Normalize();
            }

            this.RebuildSteps(keep: step);
            Game1.playSound("shiny4");
            this.EnsureVisible();
            this.UpdateRows();

            if (snap && Game1.options.SnappyMenus)
            {
                this.currentlySnappedComponent = this.getComponentWithID(RowIdBase + (this.SelectedIndex - this.ScrollOffset));
                this.snapCursorToCurrentSnappedComponent();
            }
        }

        private void GoNext()
        {
            if (this.CurrentStep == Step.Confirm)
            {
                this.Service.RequestChange(this.CurrentHorse, this.Selection);
                Game1.playSound("purchase");
                this.exitThisMenu(playSound: false);
                return;
            }
            this.StepIndex = Math.Min(this.StepIndex + 1, this.Steps.Count - 1);
            Game1.playSound("smallSelect");
            this.LoadStep();
            if (Game1.options.SnappyMenus)
                this.snapToDefaultClickableComponent();
        }

        private void GoBack()
        {
            if (this.StepIndex == 0)
                return;
            this.StepIndex--;
            Game1.playSound("shwip");
            this.LoadStep();
            if (Game1.options.SnappyMenus)
                this.snapToDefaultClickableComponent();
        }

        private void Cancel()
        {
            Game1.playSound("bigDeSelect");
            this.exitThisMenu(playSound: false);
        }

        private void Scroll(int delta, bool playSound)
        {
            if (this.CurrentStep == Step.Confirm)
                return;
            int max = Math.Max(0, this.Choices.Count - VisibleRows);
            int old = this.ScrollOffset;
            this.ScrollOffset = Math.Clamp(this.ScrollOffset + delta, 0, max);
            if (old != this.ScrollOffset)
            {
                if (playSound)
                    Game1.playSound("shiny4");
                this.UpdateRows();
            }
        }

        private void EnsureVisible()
        {
            if (this.SelectedIndex < this.ScrollOffset)
                this.ScrollOffset = this.SelectedIndex;
            else if (this.SelectedIndex >= this.ScrollOffset + VisibleRows)
                this.ScrollOffset = this.SelectedIndex - VisibleRows + 1;
            this.ScrollOffset = Math.Clamp(this.ScrollOffset, 0, Math.Max(0, this.Choices.Count - VisibleRows));
        }

        /*********
        ** Layout
        *********/
        private void Layout()
        {
            int w = Math.Min(1100, Game1.uiViewport.Width - 64);
            int h = Math.Min(720, Game1.uiViewport.Height - 160);
            int x = (Game1.uiViewport.Width - w) / 2;
            int y = (Game1.uiViewport.Height - h) / 2 + 32;
            this.initialize(x, y, w, h, showUpperRightCloseButton: true);

            int buttonY = y + h - 96;

            // fit the preview and the list between the header and the buttons
            int available = buttonY - (y + 96) - 72; // room for name/owner text under the preview
            this.PreviewScale = Math.Clamp((available - 48) / FrameSize, 4, 9);
            int previewSize = FrameSize * this.PreviewScale + 48;
            this.PreviewBox = new Rectangle(x + 48, y + 96, previewSize, previewSize);
            int listX = this.PreviewBox.Right + 40;
            int listRight = x + w - 48 - 64;
            this.FilterBar = new Rectangle(listX, this.PreviewBox.Y + 48, listRight - listX, 48);
            int listY = this.FilterBar.Bottom + 8;
            this.VisibleRows = Math.Clamp((buttonY - 40 - listY) / RowHeight, 3, 8);
            this.ListBox = new Rectangle(listX, listY, listRight - listX, this.VisibleRows * RowHeight);

            this.Rows.Clear();
            for (int i = 0; i < VisibleRows; i++)
            {
                this.Rows.Add(new ClickableComponent(new Rectangle(listX, this.ListBox.Y + i * RowHeight, this.ListBox.Width, RowHeight - 4), "row" + i)
                {
                    myID = RowIdBase + i
                });
            }

            this.UpArrow = new ClickableTextureComponent(new Rectangle(listRight + 16, this.ListBox.Y, 44, 48), Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f) { myID = UpArrowId };
            this.DownArrow = new ClickableTextureComponent(new Rectangle(listRight + 16, this.ListBox.Bottom - 48, 44, 48), Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f) { myID = DownArrowId };

            this.FilterLeft = new ClickableTextureComponent(new Rectangle(this.FilterBar.X, this.FilterBar.Y + 2, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f) { myID = FilterLeftId };
            this.FilterRight = new ClickableTextureComponent(new Rectangle(this.FilterBar.Right - 48, this.FilterBar.Y + 2, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f) { myID = FilterRightId };

            int buttonW = 200;
            this.BackButton = new ClickableComponent(new Rectangle(x + 48, buttonY, buttonW, 68), "back") { myID = BackId };
            this.CancelButton = new ClickableComponent(new Rectangle(x + w / 2 - buttonW / 2, buttonY, buttonW, 68), "cancel") { myID = CancelId };
            this.NextButton = new ClickableComponent(new Rectangle(x + w - 48 - buttonW, buttonY, buttonW, 68), "next") { myID = NextId };

            this.populateClickableComponentList();
            if (this.Steps.Count > 0)
                this.UpdateRows();
        }

        /// <summary>Show/hide rows and arrows for the current step and wire gamepad neighbours.</summary>
        private void UpdateRows()
        {
            bool list = this.CurrentStep != Step.Confirm;
            int visibleCount = list ? Math.Min(VisibleRows, this.Choices.Count - this.ScrollOffset) : 0;
            for (int i = 0; i < this.Rows.Count; i++)
                this.Rows[i].visible = i < visibleCount;
            this.UpArrow.visible = list && this.ScrollOffset > 0;
            this.DownArrow.visible = list && this.ScrollOffset + VisibleRows < this.Choices.Count;
            this.FilterLeft.visible = this.FilterRight.visible = this.HasFilter;
            this.UpdateNeighbors();
        }

        private void UpdateNeighbors()
        {
            int visibleCount = this.Rows.Count(r => r.visible);
            for (int i = 0; i < this.Rows.Count; i++)
            {
                ClickableComponent row = this.Rows[i];
                row.upNeighborID = i > 0 ? RowIdBase + i - 1 : (this.ScrollOffset > 0 || !this.HasFilter ? -7777 : FilterLeftId);
                row.downNeighborID = i < visibleCount - 1 ? RowIdBase + i + 1 : (this.DownArrow.visible ? -7777 : NextId);
                // with a collection filter, D-pad left/right on a row switches collection (see customSnapBehavior)
                row.rightNeighborID = this.HasFilter ? -7777 : (this.DownArrow.visible ? DownArrowId : (this.UpArrow.visible ? UpArrowId : -1));
                row.leftNeighborID = this.HasFilter ? -7777 : -1;
            }
            this.FilterLeft.rightNeighborID = FilterRightId;
            this.FilterLeft.downNeighborID = RowIdBase;
            this.FilterRight.leftNeighborID = FilterLeftId;
            this.FilterRight.downNeighborID = RowIdBase;
            this.UpArrow.leftNeighborID = RowIdBase;
            this.UpArrow.downNeighborID = DownArrowId;
            this.DownArrow.leftNeighborID = RowIdBase + Math.Max(0, visibleCount - 1);
            this.DownArrow.upNeighborID = UpArrowId;
            this.DownArrow.downNeighborID = NextId;

            int lastRow = visibleCount > 0 ? RowIdBase + visibleCount - 1 : -1;
            foreach (ClickableComponent button in new[] { this.BackButton, this.CancelButton, this.NextButton })
                button.upNeighborID = lastRow;
            this.BackButton.rightNeighborID = CancelId;
            this.CancelButton.leftNeighborID = BackId;
            this.CancelButton.rightNeighborID = NextId;
            this.NextButton.leftNeighborID = CancelId;
        }

        protected override void customSnapBehavior(int direction, int oldRegion, int oldID)
        {
            bool onRow = oldID >= RowIdBase && oldID < RowIdBase + this.VisibleRows;
            if (onRow && (direction == 1 || direction == 3))
            {
                this.ChangeFilter(direction == 1 ? 1 : -1);
                return;
            }
            // moving up from the first visible row scrolls the list
            if (direction == 0 && oldID == RowIdBase && this.ScrollOffset > 0)
            {
                this.Scroll(-1, playSound: true);
                this.currentlySnappedComponent = this.getComponentWithID(RowIdBase);
                this.snapCursorToCurrentSnappedComponent();
            }
            // moving down from the last visible row scrolls the list
            else if (direction == 2 && oldID >= RowIdBase && oldID < RowIdBase + this.VisibleRows)
            {
                this.Scroll(1, playSound: true);
                this.currentlySnappedComponent = this.getComponentWithID(oldID);
                this.snapCursorToCurrentSnappedComponent();
            }
        }
    }
}
