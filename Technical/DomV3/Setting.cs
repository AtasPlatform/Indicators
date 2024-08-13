namespace DomV10;

using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators;

using OFT.Attributes.Editors;

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

    #region Aggregation settings

    [Display(Name = "Show Volume Sum", Order = 10, GroupName = "Aggregation Setting")]
    public bool ShowSum
    {
	    set
	    {
		    _showSum = value;
		    RedrawChart(_emptyRedrawArg);
	    }
	    get => _showSum;
    }

    [Display(Name = "Show Order Count", Order = 20, GroupName = "Aggregation Setting")]
    public bool ShowCount
    {
	    set
	    {
		    _showCount = value;
		    RedrawChart(_emptyRedrawArg);
	    }
	    get => _showCount;
    }

    #endregion

    #region Text

    [Display(Name = "Color", GroupName = "Text", Order = 110)]
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

    #region Colors

    [Display(Name = "Bid Color", GroupName = "Colors", Order = 200)]
    public Color BidBlockColor
    {
	    get => _bidColor;
	    set
	    {
		    _bidColor = value;
		    RedrawChart(_emptyRedrawArg);
	    }
    }

    [Display(Name = "Ask Color", GroupName = "Colors", Order = 210)]
    public Color AskBlockColor
    {
	    get => _askColor;
	    set
	    {
		    _askColor = value;
		    RedrawChart(_emptyRedrawArg);
	    }
    }

    #endregion

    #region Filters

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(Name = "Order Size Filter", GroupName = "Filters", Order = 300)]
    public FilterInt OrderSizeFilter { set; get; } = new() { Enabled = true, Value = 0 };

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(Name = "Min Block Size", GroupName = "Filters", Order = 310)]
    public FilterInt MinBlockSize { set; get; } = new() { Enabled = true, Value = 1 };

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(Name = "Row Order Count ", GroupName = "Filters", Order = 320)]
    public FilterInt RowOrderCount { set; get; } = new() { Enabled = true, Value = 1 };

    [NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
    [Display(Name = "Row Order Volume ", GroupName = "Filters", Order = 330)]
    public FilterInt RowOrderVolume { set; get; } = new() { Enabled = true, Value = 1 };

    #endregion

    #endregion
}