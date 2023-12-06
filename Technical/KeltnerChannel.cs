namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Keltner Channel")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.KeltnerChannelDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602574")]
	public class KeltnerChannel : Indicator
	{
		#region Fields

		private readonly ATR _atr = new() { Period = 34 };
		private readonly RangeDataSeries _keltner = new("Keltner", "BackGround")
		{
			DrawAbovePrice = false ,
            DescriptionKey = nameof(Strings.RangeAreaDescription)
        };

		private readonly SMA _sma = new() { Period = 34 };

        private int _days = 20;
        private decimal _koef = 4;
		private int _targetBar;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
        [Range(0, 1000)]
		public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.SMAPeriodDescription),
            Order = 20)]
		[Range(1, 10000)]
        public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = _atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.OffsetMultiplier),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.ATRMultiplierDescription),
            Order = 20)]
		[Parameter]
		[Range(0.00000001, 10000000)]
        public decimal Koef
		{
			get => _koef;
			set
			{
				_koef = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KeltnerChannel()
			: base(true)
		{
			DenyToChangePanel = true;

			DataSeries.Add(new ValueDataSeries("UpperId", "Upper")
			{
				VisualType = VisualMode.Line,
				DescriptionKey = nameof(Strings.TopBandDscription)
			});

			DataSeries.Add(new ValueDataSeries("LowerId", "Lower")
			{
				VisualType = VisualMode.Line,
                DescriptionKey = nameof(Strings.BottomBandDscription)
            });

			DataSeries.Add(_keltner);
			Add(_atr);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
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

					if (_targetBar > 0)
					{
						((ValueDataSeries)DataSeries[0]).SetPointOfEndLine(_targetBar - 1);
						((ValueDataSeries)DataSeries[1]).SetPointOfEndLine(_targetBar - 1);
						((ValueDataSeries)DataSeries[2]).SetPointOfEndLine(_targetBar - 1);
					}
				}
			}

			var currentCandle = GetCandle(bar);
			var ema = _sma.Calculate(bar, currentCandle.Close);

			if (bar < _targetBar)
				return;

			var atr = _atr[bar] * Koef;
			var upAtr = ema + atr;
			var downAtr = ema - atr;

            this[bar] = ema;
			DataSeries[1][bar] = upAtr;
			DataSeries[2][bar] = downAtr;
			_keltner[bar].Upper = upAtr;
			_keltner[bar].Lower = downAtr;
		}

		#endregion
	}
}