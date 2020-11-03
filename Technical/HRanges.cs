namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Attributes;

	[DisplayName("HRanges")]
	[Category("Other")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/.3113-hranges")]
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

		private readonly ValueDataSeries _downRangeBottom = new ValueDataSeries("DownBot");
		private readonly ValueDataSeries _downRangeTop = new ValueDataSeries("DownTop");
		private readonly ValueDataSeries _flatRangeBottom = new ValueDataSeries("FlatBot");
		private readonly ValueDataSeries _flatRangeTop = new ValueDataSeries("FlatTop");
		private readonly ValueDataSeries _maxVolumeRange = new ValueDataSeries("MaxVol");
		private readonly ValueDataSeries _upRangeBottom = new ValueDataSeries("UpBot");

		private readonly ValueDataSeries _upRangeTop = new ValueDataSeries("UpTop");
		private int _currentBar = -1;
		private int _currentCountBar;
		private int _direction;
		private decimal _hRange;
		private bool _isRange;
		private int _lastBar;
		private decimal _lRange;
		private int _startingRange;
		private decimal _volumeFilter;

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

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Common")]
		public int Width
		{
			get => _upRangeTop.Width;
			set => _upRangeTop.Width = _upRangeBottom.Width = _downRangeTop.Width = _downRangeBottom.Width =
				_flatRangeTop.Width = _flatRangeBottom.Width = _maxVolumeRange.Width = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "VolumeFilter", GroupName = "Common")]
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

		#endregion

		#region ctor

		public HRanges()
			: base(true)
		{
			DenyToChangePanel = true;
			Width = 2;

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

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			_currentBar = bar - 1;

			if (_currentBar < 5 || _lastBar == _currentBar)
				return;

			_lastBar = _currentBar;

			if (IsNewSession(_currentBar))
			{
				_isRange = false;
				_direction = 0;
				_hRange = 0;
				_lRange = 0;
				_startingRange = 0;
				_currentCountBar = 0;
			}

			if (!_isRange)
			{
				if (_direction == 1 && GetCandle(_currentBar - 1).Close < GetCandle(_currentBar - 2).High)
				{
					_isRange = true;
					_hRange = Math.Max(GetCandle(_currentBar - 1).High, GetCandle(_currentBar - 2).High);
					_lRange = GetCandle(_currentBar - 1).Low;
					_startingRange = _currentBar;

					if (_currentCountBar > 0)
						_currentCountBar = -1;
					else
						_currentCountBar--;
				}
				else if (_direction == -1 && GetCandle(_currentBar - 1).Close > GetCandle(_currentBar - 2).Low)
				{
					_isRange = true;
					_hRange = GetCandle(_currentBar - 1).High;
					_lRange = Math.Min(GetCandle(_currentBar - 1).Low, GetCandle(_currentBar - 2).Low);
					_startingRange = _currentBar;

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
				if (GetCandle(_currentBar - 1).Close < _lRange && GetCandle(_currentBar - 2).Close < _lRange)
				{
					_isRange = false;

					RenderLevel(Direction.Down);

					_hRange = 0;
					_lRange = 0;
					_direction = GetLastDirection();

					Calculate(_currentBar, value);
					return;
				}

				if (GetCandle(_currentBar - 1).Close > _hRange && GetCandle(_currentBar - 2).Close > _hRange)
				{
					_isRange = false;
					RenderLevel(Direction.Up);

					_hRange = 0;
					_lRange = 0;
					_direction = GetLastDirection();
					Calculate(_currentBar, value);
					return;
				}

				if (GetCandle(_currentBar - 2).Close < _lRange && GetCandle(_currentBar - 1).Close >= _lRange)
					_lRange = Math.Min(Math.Min(GetCandle(_currentBar - 2).Low, GetCandle(_currentBar - 1).Low), _lRange);

				if (GetCandle(_currentBar - 2).Close > _hRange && GetCandle(_currentBar - 1).Close <= _hRange)
					_hRange = Math.Max(Math.Max(GetCandle(_currentBar - 2).High, GetCandle(_currentBar - 1).High), _hRange);
			}

			if (_hRange != 0 && _lRange != 0)
			{
				if (GetCandle(_currentBar - 1).Close <= _hRange)
					_hRange = Math.Max(_hRange, GetCandle(_currentBar - 1).High);

				if (GetCandle(_currentBar - 1).Close >= _lRange)
					_lRange = Math.Min(_lRange, GetCandle(_currentBar - 1).Low);

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

				for (var price = candle.High; price >= candle.Low; price -= TickSize)
				{
					var volumeInfo = candle.GetPriceVolumeInfo(price).Volume;

					if (!dict.ContainsKey(price))
						dict.Add(price, volumeInfo);
					else
						dict[price] += volumeInfo;
				}
			}

			if (dict.Count == 0)
				return;

			var maxVol = dict.Aggregate((l, r) => l.Value >= r.Value ? l : r);

			if (maxVol.Value >= VolumeFilter)
			{
				for (var i = _startingRange; i < _currentBar; i++)
					_maxVolumeRange[i] = maxVol.Key;
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