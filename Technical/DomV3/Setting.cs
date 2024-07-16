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

	private decimal _maxFontSize = 15m;

	// private Color _showCountBidColor;
	// private Color _showCountAskColor;

	// private Color _showSumBidColor;
	// private Color _showSumAskColor;

	private bool _showCount = true;

	private bool _showSum = true;
	private Color _textColor;

	#endregion

	#region Properties

	[Display(Name = "Max Size", Order = 0, GroupName = "Font Setting")]
	[NumericEditor(0.0, 100.0, Step = 0.5, DisplayFormat = "F2")]
	public decimal MaxFontSize
	{
		set
		{
			_maxFontSize = value;
			RedrawChart(_emptyRedrawArg);
		}
		get => _maxFontSize;
	}

	[Display(Name = "Show Volume Sum", Order = 100, GroupName = "Aggregation Setting")]
	public bool ShowSum
	{
		set
		{
			_showSum = value;
			RedrawChart(_emptyRedrawArg);
		}
		get => _showSum;
	}

	// [Display(Name = "Bid Color", Order = 200, GroupName = "Sum Setting")]
	// public Color ShowSumBidColor
	// {
	//     set
	//     {
	//         _showSumBidColor = value;
	//         RedrawChart(_emptyRedrawArg);
	//     }
	//     get => _showSumBidColor;
	// }
	//
	//
	// [Display(Name = "Ask Color", Order = 300, GroupName = "Sum Setting")]
	// public Color ShowSumAskColor
	// {
	//     set
	//     {
	//         _showSumAskColor = value;
	//         RedrawChart(_emptyRedrawArg);
	//     }
	//     get => _showSumAskColor;
	// }

	[Display(Name = "Show Order Count", Order = 400, GroupName = "Aggregation Setting")]
	public bool ShowCount
	{
		set
		{
			_showCount = value;
			RedrawChart(_emptyRedrawArg);
		}
		get => _showCount;
	}

	// [Display(Name = "Bid Color", Order = 500, GroupName = "Count Setting")]
	// public Color ShowCountBidColor
	// {
	//     set
	//     {
	//         _showCountBidColor = value;
	//         RedrawChart(_emptyRedrawArg);
	//     }
	//     get => _showCountBidColor;
	// }
	//
	//
	// [Display(Name = "Ask Color", Order = 600, GroupName = "Count Setting")]
	// public Color ShowCountAskColor
	// {
	//     set
	//     {
	//         _showCountAskColor = value;
	//         RedrawChart(_emptyRedrawArg);
	//     }
	//     get => _showCountAskColor;
	// }

	[Display(Name = "Bid Color", GroupName = "Profile Setting", Order = 700)]
	public Color BidBlockColor
	{
		get => _bidColor;
		set
		{
			_bidColor = value;
			RedrawChart(_emptyRedrawArg);
		}
	}

	[Display(Name = "Text Color", GroupName = "Profile Setting", Order = 800)]
	public Color TextColor
	{
		get => _textColor;
		set
		{
			_textColor = value;
			RedrawChart(_emptyRedrawArg);
		}
	}

	[IsExpanded]
	[Display(Name = "Ask Color", GroupName = "Profile Setting", Order = 900)]
	public Color AskBlockColor
	{
		get => _askColor;
		set
		{
			_askColor = value;
			RedrawChart(_emptyRedrawArg);
		}
	}

	[NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
	[Display(Name = "Order Size Filter", GroupName = "Profile Filter", Order = 1000)]
	public FilterInt OrderSizeFilter { set; get; } = new() { Enabled = true, Value = 0 };

	[NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
	[Display(Name = "Min Block Size", GroupName = "Profile Filter", Order = 1100)]
	public FilterInt MinBlockSize { set; get; } = new() { Enabled = true, Value = 1 };

	[NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
	[Display(Name = "Row Order Count ", GroupName = "Profile Filter", Order = 1200)]
	public FilterInt RowOrderCount { set; get; } = new() { Enabled = true, Value = 1 };

	[NumericEditor(0, EditorType = NumericEditorTypes.Spin, Step = 1, DisplayFormat = "F0")]
	[Display(Name = "Row Order Volume ", GroupName = "Profile Filter", Order = 1300)]
	public FilterInt RowOrderVolume { set; get; } = new() { Enabled = true, Value = 1 };

	#endregion
}