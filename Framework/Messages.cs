using System;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Farmhand -> host: please change this horse's tack.</summary>
    internal sealed class SetTackRequest
    {
        public Guid HorseId { get; set; }
        public string Coat { get; set; } = "";
        public string Style { get; set; } = "";
        public string Saddle { get; set; } = "";
        public string Pad { get; set; } = "";
        public string Bridle { get; set; } = "";

        public TackSelection ToSelection() => new TackSelection { Coat = this.Coat ?? "", Style = this.Style ?? "", Saddle = this.Saddle ?? "", Pad = this.Pad ?? "", Bridle = this.Bridle ?? "" }.Normalize();

        public static SetTackRequest From(Guid horseId, TackSelection sel) => new()
        {
            HorseId = horseId,
            Coat = sel.Coat,
            Style = sel.Style,
            Saddle = sel.Saddle,
            Pad = sel.Pad,
            Bridle = sel.Bridle
        };
    }

    /// <summary>Host -> farmhand: result of a request (translation key + token).</summary>
    internal sealed class SetTackResult
    {
        public Guid HorseId { get; set; }
        public bool Ok { get; set; }
        public string MessageKey { get; set; } = "";
        public string Arg { get; set; } = "";
    }
}
