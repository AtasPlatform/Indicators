using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Relative Vigor Index")]
	public class RelativeVigorIndex:Indicator
    {
	    private readonly ValueDataSeries _rviSeries = new ValueDataSeries("RVI");
	    private readonly ValueDataSeries _signalSeries = new ValueDataSeries(Resources.Signal);
	    private readonly SMA _smaRvi = new SMA();
	    private readonly SMA _smaSig = new SMA();

	    [Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Settings", Order = 100)]
	    public int Period
	    {
		    get => _smaSig.Period;
		    set
		    {
				if(value<=0)
					return;

				_smaSig.Period = value;
				RecalculateValues();
		    }
	    }

	    [Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
	    public int SmaPeriod
		{
		    get => _smaRvi.Period;
		    set
		    {
			    if (value <= 0)
				    return;

			    _smaRvi.Period = value;
			    RecalculateValues();
		    }
	    }

		public RelativeVigorIndex()
		    : base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_signalSeries.Color = Colors.Blue;
			_rviSeries.Color = Colors.Red;

		    _smaRvi.Period = 4;
		    _smaSig.Period = 10;

		    DataSeries[0] = _signalSeries;
			DataSeries.Add(_rviSeries);
	    }

	    protected override void OnCalculate(int bar, decimal value)
	    {
		    var candle = GetCandle(bar);

		    var rvi = 0m;

		    if (candle.High - candle.Low != 0)
			    rvi = (candle.Close - candle.Open) / (candle.High - candle.Low);

		    _rviSeries[bar] =  _smaRvi.Calculate(bar, rvi);
		    
		    _signalSeries[bar] =  _smaSig.Calculate(bar, rvi);
	    }
    }
}
