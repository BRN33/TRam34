using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Shared.DTOs.BaseDto
{
    public class BaseEntityDto
    {


        public string MessageSource { get; set; } = default!;
        public string? MessageContent { get; set; }
        public string? MessageType { get; set; }
        public DateTime DateTime { get; set; }
    }
}
