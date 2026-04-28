using System;
using System.ComponentModel.DataAnnotations;
using WalkingTec.Mvvm.Core;

namespace HWTGateway.ViewModel.BasicData.DeviceVMs
{
    public partial class DeviceApiSearcher : BaseSearcher
    {
        [Display(Name = "DeviceName")]
        public String DeviceName { get; set; }

        public Guid? DriverId { get; set; }

        protected override void InitVM()
        {
        }
    }
}