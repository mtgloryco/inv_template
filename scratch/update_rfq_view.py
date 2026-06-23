import os

file_path = "/home/itbienvenu/Documents/mwimule/PROGRAMMING/MT GLORY CO/IMV_APP/InventoryManagementSystem/InventoryManagementSystem.Shared/UI/Views/RfqView.axaml"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Normalize line endings to LF to perform match safely
content_lf = content.replace("\r\n", "\n")

actions_target = """                    <DataGridTemplateColumn Header="Actions" Width="100">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <Button Content="Edit / Open" Command="{Binding $parent[UserControl].((vm:RfqViewModel)DataContext).OpenEditRfqCommand}" CommandParameter="{Binding .}" FontSize="10" Padding="8,4" CornerRadius="3" HorizontalAlignment="Center"/>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>"""

actions_replacement = """                    <DataGridTemplateColumn Header="Actions" Width="180">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" Spacing="5" HorizontalAlignment="Center">
                                    <Button Content="Edit / Open" Command="{Binding $parent[UserControl].((vm:RfqViewModel)DataContext).OpenEditRfqCommand}" CommandParameter="{Binding .}" FontSize="10" Padding="6,4" CornerRadius="3"/>
                                    <Button Content="Print PDF" Command="{Binding $parent[UserControl].((vm:RfqViewModel)DataContext).PrintRfqCommand}" CommandParameter="{Binding .}" Background="#007ACC" Foreground="White" FontSize="10" Padding="6,4" CornerRadius="3"/>
                                </StackPanel>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>"""

width_target = 'Width="850"'
width_replacement = 'Width="1180"'

height_target = '<ScrollViewer Grid.Row="1" MaxHeight="450" HorizontalScrollBarVisibility="Disabled">'
height_replacement = '<ScrollViewer Grid.Row="1" MaxHeight="550" HorizontalScrollBarVisibility="Disabled">'

footer_target = """                    <!-- Footer Action Buttons & Total -->
                    <Border Grid.Row="2" BorderBrush="{DynamicResource BorderColor}" BorderThickness="0,1,0,0" Padding="0,15,0,0" Margin="0,20,0,0">
                        <Grid ColumnDefinitions="Auto, *">
                            <StackPanel Orientation="Horizontal" Spacing="15" VerticalAlignment="Center">
                                <TextBlock Text="Total Amount:" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                <TextBlock Text="{Binding RfqTotalAmount, StringFormat='{}{0:N2}'}" FontSize="18" FontWeight="Bold" Foreground="#007ACC" VerticalAlignment="Center"/>
                                <TextBlock Text="{Binding CurrentRfq.Currency}" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
                            </StackPanel>
                            
                            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="10" HorizontalAlignment="Right">
                                <Button Content="Cancel" Command="{Binding CancelFormCommand}"/>
                                <Button Content="Save Draft" Command="{Binding SaveRfqCommand}" Background="#F57C00" Foreground="White"/>
                                <Button Content="Confirm Order" Command="{Binding ConfirmAsPurchaseOrderCommand}" Background="#2E7D32" Foreground="White" IsVisible="{Binding CurrentRfq.Id}"/>
                            </StackPanel>
                        </Grid>
                    </Border>"""

footer_replacement = """                    <!-- Footer Action Buttons & Total -->
                    <Border Grid.Row="2" BorderBrush="{DynamicResource BorderColor}" BorderThickness="0,1,0,0" Padding="0,15,0,0" Margin="0,20,0,0">
                        <Grid ColumnDefinitions="Auto, *">
                            <StackPanel Orientation="Vertical" Spacing="4" VerticalAlignment="Center">
                                <StackPanel Orientation="Horizontal" Spacing="10">
                                    <TextBlock Text="Sub Total:" FontSize="12" Foreground="Gray" VerticalAlignment="Center"/>
                                    <TextBlock Text="{Binding RfqSubtotalAmount, StringFormat='{}{0:N2}'}" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                    <TextBlock Text="{Binding CurrentRfq.Currency}" FontSize="12" Foreground="Gray" VerticalAlignment="Center"/>
                                </StackPanel>
                                <TextBlock Text="{Binding RfqTaxBreakdownText}" FontSize="11" Foreground="Gray" TextWrapping="Wrap" MaxWidth="400"/>
                                <StackPanel Orientation="Horizontal" Spacing="10" Margin="0,5,0,0">
                                    <TextBlock Text="Total Amount:" FontSize="16" FontWeight="Bold" VerticalAlignment="Center"/>
                                    <TextBlock Text="{Binding RfqTotalAmount, StringFormat='{}{0:N2}'}" FontSize="18" FontWeight="Bold" Foreground="#007ACC" VerticalAlignment="Center"/>
                                    <TextBlock Text="{Binding CurrentRfq.Currency}" FontSize="16" FontWeight="Bold" VerticalAlignment="Center"/>
                                </StackPanel>
                            </StackPanel>
                            
                            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="10" HorizontalAlignment="Right" VerticalAlignment="Bottom">
                                <Button Content="Cancel" Command="{Binding CancelFormCommand}"/>
                                <Button Content="Preview PDF" Command="{Binding PreviewCurrentRfqPdfCommand}" Background="#007ACC" Foreground="White"/>
                                <Button Content="Save Draft" Command="{Binding SaveRfqCommand}" Background="#F57C00" Foreground="White"/>
                                <Button Content="Confirm Order" Command="{Binding ConfirmAsPurchaseOrderCommand}" Background="#2E7D32" Foreground="White" IsVisible="{Binding CurrentRfq.Id}"/>
                            </StackPanel>
                        </Grid>
                    </Border>"""

# Match on target strings normalized to LF
if actions_target in content_lf and footer_target in content_lf:
    content_lf = content_lf.replace(actions_target, actions_replacement)
    content_lf = content_lf.replace(width_target, width_replacement)
    content_lf = content_lf.replace(height_target, height_replacement)
    content_lf = content_lf.replace(footer_target, footer_replacement)
    
    # Restore original line endings (CRLF if the original had them)
    if "\r\n" in content:
        content_lf = content_lf.replace("\n", "\r\n")
        
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(content_lf)
    print("Success")
else:
    print("Failed to match targets in RfqView.axaml")
