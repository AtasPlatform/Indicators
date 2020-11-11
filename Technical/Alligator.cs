namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Alligator")]
	[Description("Alligator by Bill Williams")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17027-alligator")]
	public class Alligator : Indicator
	{
		#region Fields

		private readonly SMMA _jaw = new SMMA();
		private readonly SMMA _lips = new SMMA();
		private readonly SMMA _teeth = new SMMA();

		private int _jawShift;
		private int _lipsShift;
		private int _teethShift;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "JawAlligator", Order = 0)]
		public int JawPeriod
		{
			get => _jaw.Period;
			set
			{
				_jaw.Period = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Shift", GroupName = "JawAlligator", Order = 1)]
		public int JawShift
		{
			get => _jawShift;
			set
			{
				_jawShift = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "TeethAlligator", Order = 0)]
		public int TeethPeriod
		{
			get => _teeth.Period;
			set
			{
				_teeth.Period = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Shift", GroupName = "TeethAlligator", Order = 1)]
		public int TeethShift
		{
			get => _teethShift;
			set
			{
				_teethShift = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "LipsAlligator", Order = 0)]
		public int LipsPeriod
		{
			get => _lips.Period;
			set
			{
				_lips.Period = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Shift", GroupName = "LipsAlligator", Order = 1)]
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
			((ValueDataSeries)DataSeries[0]).Color = Colors.Blue;

			DataSeries.Add(new ValueDataSeries("Teeth")
			{
				VisualType = VisualMode.Line,
				ShowZeroValue = false,
				Color = Colors.Red
			});

			DataSeries.Add(new ValueDataSeries("Lips")
			{
				VisualType = VisualMode.Line,
				ShowZeroValue = false,
				Color = Colors.Green
			});

			JawPeriod = 13;
			TeethPeriod = 8;
			LipsPeriod = 5;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				((ValueDataSeries)DataSeries[0]).SetPointOfEndLine(LastVisibleBarNumber + _jawShift);
				((ValueDataSeries)DataSeries[1]).SetPointOfEndLine(LastVisibleBarNumber + _teethShift);
				((ValueDataSeries)DataSeries[2]).SetPointOfEndLine(LastVisibleBarNumber + _lipsShift);
			}

			var average = (GetCandle(bar).Low + GetCandle(bar).High) / 2;

			if (bar < _jawShift)
				this[bar] = average;
			else
			{
				if (bar - _jawShift <= LastVisibleBarNumber)
					this[bar] = _jaw.Calculate(bar - _jawShift, (GetCandle(bar - _jawShift).Low + GetCandle(bar - _jawShift).High) / 2);
			}

			if (bar < _teethShift)
				DataSeries[1][bar] = average;
			else
			{
				if (bar - _teethShift <= LastVisibleBarNumber)
					DataSeries[1][bar] = _teeth.Calculate(bar - _teethShift, (GetCandle(bar - _teethShift).Low + GetCandle(bar - _teethShift).High) / 2);
			}

			if (bar < _lipsShift)
				DataSeries[2][bar] = average;
			else
			{
				if (bar - _lipsShift <= LastVisibleBarNumber)
					DataSeries[2][bar] = _lips.Calculate(bar - _lipsShift, (GetCandle(bar - _lipsShift).Low + GetCandle(bar - _lipsShift).High) / 2);
			}
		}

		#endregion
	}
}