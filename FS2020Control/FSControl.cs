using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// https://learn.microsoft.com/en-us/ef/core/get-started/wpf

namespace FS2020Control
{
  public class FSControlFile
  {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public int FSControlFileId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string FriendlyName { get; set; } = default!;
    public string Device { get; set; } = default!;

    public virtual ICollection<FSControl> FSControls { get; set; } =
      new ObservableCollection<FSControl>();
  }

  public class FSControl
  {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public int FSControlId { get; set; } = default!;
    public string ContextName { get; set; } = default!;
    public string ActionName { get; set; } = default!;
    public string? Actor { get; set; } = default!;
    public string? FriendlyAction { get; set; } = default!;
    public string PrimaryKeys { get; set; } = default!;
    public string PrimaryKeysCode { get; set; } = default!;
    public string? SecondaryKeys { get; set; } = default!;
    public string? SecondaryKeysCode { get; set; } = default!;

    public int FSControlFileId { get; set; }
    public virtual FSControlFile FSControlFile { get; set; } = default!;
  }
}