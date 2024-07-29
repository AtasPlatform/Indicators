namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Alligator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AlligatorDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602579")]
	public class Alligator : Indicator
	{
		#region Fields

		private readonly SMMA _jaw = new();
		private readonly SMMA _lips = new();
		private readonly SMMA _teeth = new();

		private int _jawShift;
		private int _lipsShift;
		private int _teethShift;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.JawAlligator), Description = nameof(Strings.LongPeriodDescription), Order = 0)]
		public int JawPeriod
		{
			get => _jaw.Period;
			set
			{
				_jaw.Period = Math.Max(1, value);
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Shift), GroupName = nameof(Strings.JawAlligator), Description = nameof(Strings.BarShiftDescription), Order = 1)]
		public int JawShift
		{
			get => _jawShift;
			set
			{
				_jawShift = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.TeethAlligator), Description = nameof(Strings.MediumPeriodDescription), Order = 0)]
		public int TeethPeriod
		{
			get => _teeth.Period;
			set
			{
				_teeth.Period = Math.Max(1, value);
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Shift), GroupName = nameof(Strings.TeethAlligator), Description = nameof(Strings.BarShiftDescription), Order = 1)]
		public int TeethShift
		{
			get => _teethShift;
			set
			{
				_teethShift = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.LipsAlligator), Description = nameof(Strings.ShortPeriodDescription), Order = 0)]
		public int LipsPeriod
		{
			get => _lips.Period;
			set
			{
				_lips.Period = Math.Max(1, value);
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Shift), GroupName = nameof(Strings.LipsAlligator), Description = nameof(Strings.BarShiftDescription), Order = 1)]
		public int LipsShift
		{
			get => _lipsShift;
			set
			{
				_lipsShift = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Alligator()
		{
			_jawShift = 8;
			_teethShift = 5;
			_lipsShift = 3;

			((ValueDataSeries)DataSeries[0]).Name = "Jaw";
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Line;
			((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Blue.Convert();

            DataSeries.Add(new ValueDataSeries("TeethId", "Teeth")
			{
				VisualType = VisualMode.Line,
				ShowZeroValue = false,
				Color = DefaultColors.Red.Convert()
			});

			DataSeries.Add(new ValueDataSeries("LipsId", "Lips")
			{
				VisualType = VisualMode.Line,
				ShowZeroValue = false,
				Color = DefaultColors.Green.Convert()
            });

			JawPeriod = 13;
			TeethPeriod = 8;
			LipsPeriod = 5;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var average = (GetCandle(bar).Low + GetCandle(bar).High) / 2;

			if (bar < _jawShift)
				this[bar] = average;
			else
			{
				if (bar - _jawShift <= CurrentBar - 1)
					this[bar] = _jaw.Calculate(bar - _jawShift, (GetCandle(bar - _jawShift).Low + GetCandle(bar - _jawShift).High) / 2);
			}

			if (bar < _teethShift)
				DataSeries[1][bar] = average;
			else
			{
				if (bar - _teethShift <= CurrentBar - 1)
					DataSeries[1][bar] = _teeth.Calculate(bar - _teethShift, (GetCandle(bar - _teethShift).Low + GetCandle(bar - _teethShift).High) / 2);
			}

			if (bar < _lipsShift)
				DataSeries[2][bar] = average;
			else
			{
				if (bar - _lipsShift <= CurrentBar - 1)
					DataSeries[2][bar] = _lips.Calculate(bar - _lipsShift, (GetCandle(bar - _lipsShift).Low + GetCandle(bar - _lipsShift).High) / 2);
			}
		}

		#endregion
	}
}