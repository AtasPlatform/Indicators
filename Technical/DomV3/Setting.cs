namespace DomV10;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators;

using Newtonsoft.Json;

using OFT.Attributes.Editors;
using OFT.Localization;
using OFT.Rendering.Tools;

public partial class MainIndicator
{
	#region Nested types

	// PLAT-5080: a size filter that may be set in money. Templates keep the old FilterInt object (older versions read it);
	// the exact threshold and the money part are stored beside it. The two filters are kept in sync both ways
	private sealed class VolumeFilterIntLink
	{
		#region Fields

		private FilterInt _persisted;
		private VolumeFilter _volume;
		private bool _syncing;

		#endregion

		#region Properties

		public FilterInt Persisted
		{
			get => _persisted;
			set
			{
				if (value is null || ReferenceEquals(_persisted, value))
					return;

				if (_persisted is not null)
					_persisted.PropertyChanged -= OnPersistedChanged;

				_persisted = value;
				_persisted.PropertyChanged += OnPersistedChanged;
				OnPersistedChanged(this, null);
			}
		}

		public VolumeFilter Volume
		{
			get => _volume;
			set
			{
				if (value is null || ReferenceEquals(_volume, value))
					return;

				if (_volume is not null)
					_volume.PropertyChanged -= OnVolumeChanged;

				_volume = value;
				_volume.PropertyChanged += OnVolumeChanged;
				OnVolumeChanged(this, null);
			}
		}

		#endregion

		#region Constructors

		public VolumeFilterIntLink(int value)
		{
			Volume = new VolumeFilter(true) { Enabled = true, Value = value };
			Persisted = new FilterInt { Enabled = true, Value = value };
		}

		#endregion

		#region Private methods

		private void OnPersistedChanged(object sender, PropertyChangedEventArgs e)
		{
			if (_syncing || _volume is null)
				return;

			_syncing = true;

			try
			{
				_volume.Enabled = _persisted.Enabled;

				// the rounded exact threshold is not overwritten by its own integer part
				if ((int)Math.Round(_volume.Value) != _persisted.Value)
					_volume.Value = _persisted.Value;
			}
			finally
			{
				_syncing = false;
			}
		}

		private void OnVolumeChanged(object sender, PropertyChangedEventArgs e)
		{
			if (_syncing || _persisted is null)
				return;

			_syncing = true;

			try
			{
				_persisted.Enabled = _volume.Enabled;
				_persisted.Value = (int)Math.Round(_volume.Value);
			}
			finally
			{
				_syncing = false;
			}
		}

		#endregion
	}

	#endregion

	#region Fields

	private readonly VolumeFilterIntLink _orderSize = new(2);
	private readonly VolumeFilterIntLink _minBlockSize = new(2);
	private readonly VolumeFilterIntLink _rowOrderVolume = new(1);

	private readonly RedrawArg _emptyRedrawArg = new(new Rectangle(0, 0, 0, 0));
	private Color _askColor;
	private RenderPen _askColorPen;
	private Color _bidColor;
	private RenderPen _bidColorPen;
	private bool _showCount = true;
	private bool _showSum = true;
	private Color _textColor;
	private RenderPen _textColorPen;

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
			_bidColorPen = new RenderPen(value);
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
			_askColorPen = new RenderPen(value);
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
		    _textColorPen = new RenderPen(value);
			RedrawChart(_emptyRedrawArg);
	    }
    }

    #endregion

    #region Filters

    // PLAT-5080: the order and row size filters may be set in money; compared at the order price and the row middle price
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColorFilter), GroupName = nameof(Strings.MBOFilters), Order = 100)]
    [JsonIgnore]
    public VolumeFilter OrderSizeVolumeFilter
    {
        get => _orderSize.Volume;
        set => _orderSize.Volume = value;
    }

    [Browsable(false)]
    public FilterInt OrderSizeFilter
    {
        get => _orderSize.Persisted;
        set => _orderSize.Persisted = value;
    }

    // declared after OrderSizeFilter: loaded last, it restores a fractional threshold; older versions ignore it
    [Browsable(false)]
    public decimal OrderSizeFilterExact
    {
        get => _orderSize.Volume.Value;
        set => _orderSize.Volume.Value = value;
    }

    [Browsable(false)]
    public string? OrderSizeFilterMoney
    {
        get => _orderSize.Volume.MoneyScalar;
        set => _orderSize.Volume.MoneyScalar = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TotalVolumeFilter), GroupName = nameof(Strings.MBOFilters), Order = 110)]
    [JsonIgnore]
    public VolumeFilter MinBlockSizeFilter
    {
        get => _minBlockSize.Volume;
        set => _minBlockSize.Volume = value;
    }

    [Browsable(false)]
    public FilterInt MinBlockSize
    {
        get => _minBlockSize.Persisted;
        set => _minBlockSize.Persisted = value;
    }

    // declared after MinBlockSize: loaded last, it restores a fractional threshold; older versions ignore it
    [Browsable(false)]
    public decimal MinBlockSizeExact
    {
        get => _minBlockSize.Volume.Value;
        set => _minBlockSize.Volume.Value = value;
    }

    [Browsable(false)]
    public string? MinBlockSizeMoney
    {
        get => _minBlockSize.Volume.MoneyScalar;
        set => _minBlockSize.Volume.MoneyScalar = value;
    }

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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TotalVolumeFilter), GroupName = nameof(Strings.Summary), Order = 240)]
    [JsonIgnore]
    public VolumeFilter RowOrderVolumeFilter
    {
        get => _rowOrderVolume.Volume;
        set => _rowOrderVolume.Volume = value;
    }

    [Browsable(false)]
    public FilterInt RowOrderVolume
    {
        get => _rowOrderVolume.Persisted;
        set => _rowOrderVolume.Persisted = value;
    }

    // declared after RowOrderVolume: loaded last, it restores a fractional threshold; older versions ignore it
    [Browsable(false)]
    public decimal RowOrderVolumeExact
    {
        get => _rowOrderVolume.Volume.Value;
        set => _rowOrderVolume.Volume.Value = value;
    }

    [Browsable(false)]
    public string? RowOrderVolumeMoney
    {
        get => _rowOrderVolume.Volume.MoneyScalar;
        set => _rowOrderVolume.Volume.MoneyScalar = value;
    }

    #endregion

    #endregion
}