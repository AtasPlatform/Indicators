namespace DomV10;

using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators;

using OFT.Attributes.Editors;
using OFT.Localization;

public partial class MainIndicator
{
	#region Fields

	private readonly RedrawArg _emptyRedrawArg = new(new Rectangle(0, 0, 0, 0));
	private Color _askColor;
	private Color _bidColor;
	private bool _showCount = true;
	private bool _showSum = true;
	private Color _textColor;

    #endregion

    #region Properties

    #region Colors

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bids), GroupName = nameof(Strings.Colors),Order = 2)]
    public Color BidBlockColor
    {
	    get => _bidColor;
	    set
	    {
		    _bidColor = value;
		    RedrawChart(_emptyRedrawArg);
	    }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Asks), GroupName = nameof(Strings.Colors), Order = 4)]
    public Color AskBlockColor
    {
	    get => _askColor;
	    set
	    {
		    _askColor = value;
		    RedrawChart(_emptyRedrawArg);
	    }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Colors), Order = 6)]
    public Color TextColor
    {
	    get => _textColor;
	    set
	    {
		    _textColor = value;
		    RedrawChart(_emptyRedrawArg);
	    }
    }

    #endregion

    #region Filters

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColorFilter), GroupName = nameof(Strings.MBOFilters), Order = 100)]
    public FilterInt OrderSizeFilter { set; get; } = new() { Enabled = true, Value = 0 };

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TotalVolumeFilter), GroupName = nameof(Strings.MBOFilters), Order = 110)]
    public FilterInt MinBlockSize { set; get; } = new() { Enabled = true, Value = 1 };

    #endregion

    #region Aggregation settings

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolume), GroupName = nameof(Strings.Summary), Order = 210)]
    public bool ShowSum
    {
	    set
	    {
		    _showSum = value;
		    RedrawChart(_emptyRedrawArg);
	    }
	    get => _showSum;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowOrdersCount), GroupName = nameof(Strings.Summary), Order = 220)]
    public bool ShowCount
    {
	    set
	    {
		    _showCount = value;
		    RedrawChart(_emptyRedrawArg);
	    }
	    get => _showCount;
    }

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OrdersCountFilter), GroupName = nameof(Strings.Summary), Order = 230)]
    public FilterInt RowOrderCount { set; get; } = new() { Enabled = true, Value = 1 };

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TotalVolumeFilter), GroupName = nameof(Strings.Summary), Order = 240)]
    public FilterInt RowOrderVolume { set; get; } = new() { Enabled = true, Value = 1 };

    #endregion

    #endregion
}