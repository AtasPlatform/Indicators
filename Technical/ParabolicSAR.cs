namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("Parabolic SAR")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ParabolicSARDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602442")]
	public class ParabolicSAR : Indicator
	{
		#region Fields

        private decimal _start = 0.02m;
        private decimal _increment = 0.02m;
        private decimal _max = 0.2m;

        private decimal _sar;
        private decimal _acceleration;
        private decimal _extreme;
        private bool _isUptrend;
        private int _lastBar;

        #endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.AccelStart),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.AccelStartDescription),
            Order = 20)]
		[Range(0.000000001, 100000000)]
		public decimal AccelStart
		{
			get => _start;
			set
			{
				_start = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.AccelStep),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.AccelStepDescription),
            Order = 21)]
		[Range(0.000000001, 100000000)]
        public decimal AccelStep
		{
			get => _increment;
			set
			{
				_increment = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.AccelMax),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.AccelMaxDescription),
            Order = 22)]
		[Range(0.000000001, 100000000)]
        public decimal AccelMax
		{
			get => _max;
			set
			{
				_max = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ParabolicSAR()
			: base(true)
		{
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Dots;
			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Blue.Convert();
			((ValueDataSeries)DataSeries[0]).Width = 2;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_sar = _acceleration = _extreme = 0;
				_isUptrend = false;
				_lastBar = 0;
			}

			if (bar < 2)
				return;

			if (_lastBar == bar)
				return;

			_lastBar = bar;

            bar--;

			var high = GetCandle(bar).High;
			var low = GetCandle(bar).Low;
			var prevHigh = GetCandle(bar - 1).High;
			var prevLow = GetCandle(bar - 1).Low;

			if (bar == 1)
			{
				_sar = _isUptrend ? prevLow : prevHigh;
				_extreme = _isUptrend ? high : low;
				return;
			}

			_sar += _acceleration * (_extreme - _sar);

			if (_isUptrend)
			{
				if (_sar > low)
				{
					_isUptrend = false;
					_sar = Math.Max(prevHigh, _extreme);
					_extreme = low;
					_acceleration = _start;
				}
				else
				{
					if (high > _extreme)
					{
						_extreme = high;
						_acceleration = Math.Min(_acceleration + _increment, _max);
					}
				}
			}
			else
			{
				if (_sar < high)
				{
					_isUptrend = true;
					_sar = Math.Min(prevLow, _extreme);
					_extreme = high;
					_acceleration = _start;
				}
				else
				{
					if (low < _extreme)
					{
						_extreme = low;
						_acceleration = Math.Min(_acceleration + _increment, _max);
					}
				}
			}

			_sar = _isUptrend 
				? Math.Min(_sar, Math.Min(prevLow, GetCandle(bar - 2).Low))
				: Math.Max(_sar, Math.Max(prevHigh, GetCandle(bar - 2).High));

			this[bar] = _sar;
		}

		#endregion
	}
}
