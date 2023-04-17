namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Collections;

	[DisplayName("HRanges")]
	[Category("Other")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/3113-hranges")]
	public class HRanges : Indicator
	{
		#region Nested types

		public enum Direction
		{
			Up,
			Down,
			Flat
		}

		#endregion

		#region Fields
		private readonly Dictionary<int, IEnumerable<PriceVolumeInfo>> _priceVolumeInfoCache = new();

		private readonly ValueDataSeries _downRangeBottom = new("DownBot");
		private readonly ValueDataSeries _downRangeTop = new("DownTop");
		private readonly ValueDataSeries _flatRangeBottom = new("FlatBot");
		private readonly ValueDataSeries _flatRangeTop = new("FlatTop");
		private readonly ValueDataSeries _maxVolumeRange = new("MaxVol");
		private readonly ValueDataSeries _upRangeBottom = new("UpBot");

		private readonly ValueDataSeries _upRangeTop = new("UpTop");
		private int _currentBar = -1;
		private int _currentCountBar;
		private int _days;
		private int _direction;
		private decimal _hRange;
		private bool _isRange;
		private int _lastBar;
		private decimal _lRange;
		private int _startingRange;
		private int _targetBar;
		private decimal _volumeFilter;
		private bool _hideAllBarsFilter;
		private int _barsRange;
		private bool _hideAllVolume;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BreakUpColor", GroupName = "Colors")]
		public Color SwingUpColor
		{
			get => _upRangeTop.Color;
			set => _upRangeTop.Color = _upRangeBottom.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "MaxVolColor", GroupName = "Colors")]
		public Color VolumeColor
		{
			get => _maxVolumeRange.Color;
			set => _maxVolumeRange.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "BreakDnColor", GroupName = "Colors")]
		public Color SwingDnColor
		{
			get => _downRangeTop.Color;
			set => _downRangeTop.Color = _downRangeBottom.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "FlatColor", GroupName = "Colors")]
		public Color NeutralColor
		{
			get => _flatRangeTop.Color;
			set => _flatRangeTop.Color = _flatRangeBottom.Color = value;
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "DaysLookBack", Order = int.MaxValue, Description = "DaysLookBackDescription")]
        public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Common")]
		public int Width
		{
			get => _upRangeTop.Width;
			set => _upRangeTop.Width = _upRangeBottom.Width = _downRangeTop.Width = _downRangeBottom.Width =
				_flatRangeTop.Width = _flatRangeBottom.Width = _maxVolumeRange.Width = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "VolumeFilter")]
		public decimal VolumeFilter
		{
			get => _volumeFilter;
			set
			{
				if (value < 0)
					return;

				_volumeFilter = value;
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Resources), Name = "HideAll", GroupName = "VolumeFilter")]
		public bool HideAllVolume
		{
			get => _hideAllVolume;
			set
			{
				_hideAllVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "BarsCountFilter")]
		public int BarsRange
		{
			get => _barsRange;
			set
			{
				if (value < 0)
					return;

				_barsRange = value;
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Resources), Name = "HideAll", GroupName = "BarsCountFilter")]
		public bool HideAllBarsFilter
		{
			get => _hideAllBarsFilter;
			set
			{
				_hideAllBarsFilter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HRanges()
			: base(true)
		{
			DenyToChangePanel = true;
			Width = 2;
			_days = 20;

			_upRangeTop.Color = _upRangeBottom.Color = Colors.Green;
			_downRangeTop.Color = _downRangeBottom.Color = Colors.Red;
			_flatRangeTop.Color = _flatRangeBottom.Color = Colors.Gray;
			_maxVolumeRange.Color = Colors.DodgerBlue;

			_upRangeTop.IsHidden = _upRangeBottom.IsHidden = true;
			_downRangeTop.IsHidden = _downRangeBottom.IsHidden = true;
			_flatRangeTop.IsHidden = _flatRangeBottom.IsHidden = true;
			_maxVolumeRange.IsHidden = true;

			_upRangeTop.ShowZeroValue = _upRangeBottom.ShowZeroValue = false;
			_downRangeTop.ShowZeroValue = _downRangeBottom.ShowZeroValue = false;
			_flatRangeTop.ShowZeroValue = _flatRangeBottom.ShowZeroValue = false;
			_maxVolumeRange.ShowZeroValue = false;

			_upRangeTop.VisualType = _upRangeBottom.VisualType = VisualMode.Hash;
			_downRangeTop.VisualType = _downRangeBottom.VisualType = VisualMode.Hash;
			_flatRangeTop.VisualType = _flatRangeBottom.VisualType = VisualMode.Hash;
			_maxVolumeRange.VisualType = VisualMode.Hash;

			DataSeries[0] = _upRangeTop;
			DataSeries.Add(_upRangeBottom);
			DataSeries.Add(_downRangeTop);
			DataSeries.Add(_downRangeBottom);
			DataSeries.Add(_flatRangeTop);
			DataSeries.Add(_flatRangeBottom);
			DataSeries.Add(_maxVolumeRange);
		}

        #endregion

        #region Protected methods

        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

	        SwingUpColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	        SwingDnColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
	        NeutralColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        }

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_isRange = false;
				_direction = 0;
				_hRange = 0;
				_lRange = 0;
				_startingRange = 0;
				_currentCountBar = 0;

				_targetBar = 0;

				if (_days > 0)
				{
					var days = 0;

					for (var i = CurrentBar - 1; i >= 0; i--)
					{
						_targetBar = i;

						if (!IsNewSession(i))
							continue;

						days++;

						if (days == _days)
							break;
					}
				}
			}

			_currentBar = bar - 1;

			if (_currentBar < 5 || _lastBar == _currentBar)
				return;

			_lastBar = bar;

			if (bar < _targetBar)
				return;

			if (IsNewSession(_currentBar))
			{
				_isRange = false;
				_direction = 0;
				_hRange = 0;
				_lRange = 0;
				_startingRange = 0;
				_currentCountBar = 0;
			}

			var candle = GetCandle(_currentBar - 1);
			var prevCandle = GetCandle(_currentBar - 2);

			if (!_isRange)
			{
				if (_direction == 1 && candle.Close < prevCandle.High)
				{
					_isRange = true;
					_hRange = Math.Max(candle.High, prevCandle.High);
					_lRange = candle.Low;
					_startingRange = _currentBar;

					_priceVolumeInfoCache.RemoveWhere(x => x.Key < _startingRange);

					if (_currentCountBar > 0)
						_currentCountBar = -1;
					else
						_currentCountBar--;
				}
				else if (_direction == -1 && candle.Close > prevCandle.Low)
				{
					_isRange = true;
					_hRange = candle.High;
					_lRange = Math.Min(candle.Low, prevCandle.Low);
					_startingRange = _currentBar;

					_priceVolumeInfoCache.RemoveWhere(x => x.Key < _startingRange);

					if (_currentCountBar < 0)
						_currentCountBar = 1;
					else
						_currentCountBar++;
				}
				else
					_direction = GetLastDirection();
			}

			if (_isRange && _currentBar - _startingRange >= 2)
			{
				if (candle.Close < _lRange && prevCandle.Close < _lRange)
				{
					_isRange = false;

					RenderLevel(Direction.Down);

					_hRange = 0;
					_lRange = 0;
					_direction = GetLastDirection();

					Calculate(_currentBar, value);
					return;
				}

				if (candle.Close > _hRange && prevCandle.Close > _hRange)
				{
					_isRange = false;
					RenderLevel(Direction.Up);

					_hRange = 0;
					_lRange = 0;
					_direction = GetLastDirection();
					Calculate(_currentBar, value);
					return;
				}

				if (prevCandle.Close < _lRange && candle.Close >= _lRange)
					_lRange = Math.Min(Math.Min(prevCandle.Low, candle.Low), _lRange);

				if (prevCandle.Close > _hRange && candle.Close <= _hRange)
					_hRange = Math.Max(Math.Max(prevCandle.High, candle.High), _hRange);
			}

			if (_hRange != 0 && _lRange != 0)
			{
				if (candle.Close <= _hRange)
					_hRange = Math.Max(_hRange, candle.High);

				if (candle.Close >= _lRange)
					_lRange = Math.Min(_lRange, candle.Low);

				RenderLevel(Direction.Flat);
			}
		}

		#endregion

		#region Private methods

		private void RenderLevel(Direction direction)
		{
			var dict = new Dictionary<decimal, decimal>();

			for (var i = _startingRange; i < _currentBar; i++)
			{
				switch (direction)
				{
					case Direction.Up:
						_upRangeTop[i] = _hRange;
						_upRangeBottom[i] = _lRange;
						_flatRangeTop[i] = 0;
						_flatRangeBottom[i] = 0;
						break;

					case Direction.Down:
						_downRangeTop[i] = _hRange;
						_downRangeBottom[i] = _lRange;
						_flatRangeTop[i] = 0;
						_flatRangeBottom[i] = 0;
						break;

					case Direction.Flat:
						_flatRangeTop[i] = _hRange;
						_flatRangeBottom[i] = _lRange;
						break;
				}

				var candle = GetCandle(i);
				var volumeInfos = i != CurrentBar - 1
					? _priceVolumeInfoCache.GetOrAdd(i, _ => candle.GetAllPriceLevels())
					: candle.GetAllPriceLevels();

				foreach (var volumeInfo in volumeInfos)
					dict.IncrementValue(volumeInfo.Price, volumeInfo.Volume);
			}

			if (dict.Count == 0)
				return;

			var maxVol = dict.Aggregate((l, r) => l.Value >= r.Value ? l : r);

			if (maxVol.Value >= VolumeFilter && _currentBar - _startingRange >= BarsRange)
			{
				for (var i = _startingRange; i < _currentBar; i++)
					_maxVolumeRange[i] = maxVol.Key;
			}
			else 
			{
				if (HideAllBarsFilter && _currentBar - _startingRange < BarsRange || HideAllVolume && maxVol.Value < VolumeFilter)
					for (var i = _startingRange; i < _currentBar; i++)
						DataSeries.ForEach(x => ((ValueDataSeries)x)[i] = 0);
				else
					for (var i = _startingRange; i < _currentBar; i++)
						_maxVolumeRange[i] = 0;
			}
		}

		private int GetLastDirection()
		{
			for (var i = _currentBar - 1; i > 0; i--)
			{
				var candle = GetCandle(i);
				var prevCandle = GetCandle(i - 1);

				if (candle.Close > prevCandle.High)
					return 1;

				if (candle.Close < prevCandle.Low)
					return -1;
			}

			return 0;
		}

		#endregion
	}
}