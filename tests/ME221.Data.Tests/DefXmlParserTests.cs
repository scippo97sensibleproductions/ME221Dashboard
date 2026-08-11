using FluentAssertions;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class DefXmlParserTests
{
    private const string FullDefXml = """
        <ecu>
          <DeviceDataInformationModel>
            <ProductName>ME221</ProductName>
            <ModelName>V7 Test</ModelName>
            <Version>1.2.3</Version>
          </DeviceDataInformationModel>
          <links>
            <DataLinkModel>
              <id>1</id>
              <name>Engine RPM</name>
              <category>Engine</category>
              <ViewInTree>true</ViewInTree>
              <StandardLogging>true</StandardLogging>
              <measureUnit>RPM</measureUnit>
              <MeasurementUnitTypes><Rpm /></MeasurementUnitTypes>
              <DataTypeSet>Normal</DataTypeSet>
              <MinValue>0</MinValue>
              <MaxValue>9000</MaxValue>
              <DataKey>rpm</DataKey>
            </DataLinkModel>
            <DataLinkModel>
              <id>2</id>
              <name>Coolant Temp</name>
              <category>Engine</category>
              <MeasurementUnitTypes><Celsius /></MeasurementUnitTypes>
              <DataTypeSet>Percent</DataTypeSet>
              <MinValue>-40</MinValue>
              <MaxValue>150</MaxValue>
              <TextValues>
                <TextValueModel><value>0</value><text>Cold</text></TextValueModel>
                <TextValueModel><value>100</value><text>Hot</text></TextValueModel>
              </TextValues>
              <Feedbacks>
                <Ok>
                  <Constraints>
                    <Gt Value="-40" />
                    <Lt Value="100" />
                  </Constraints>
                </Ok>
                <Warning Flashing="true">
                  <Constraints>
                    <GtEq Value="100" />
                    <Lt Value="120" />
                  </Constraints>
                </Warning>
                <Alarm>
                  <Constraints>
                    <GtEq Value="120" />
                    <Eq Value="3 4" />
                  </Constraints>
                </Alarm>
              </Feedbacks>
            </DataLinkModel>
            <DataLinkModel>
              <id>3</id>
              <name>Unknown Unit Link</name>
              <MeasurementUnitTypes><CustomThing /></MeasurementUnitTypes>
            </DataLinkModel>
          </links>
          <tables>
            <TableModel>
              <id>10</id>
              <name>Ignition Map</name>
              <category>Ignition</category>
              <ViewInTree>true</ViewInTree>
              <enabled>true</enabled>
              <type>T16x16</type>
              <cols>16</cols>
              <rows>16</rows>
              <input_0_LinkId>1</input_0_LinkId>
              <input_1_LinkId>2</input_1_LinkId>
              <output_LinkId>3</output_LinkId>
              <input_0_name>RPM</input_0_name>
              <input_1_name>Load</input_1_name>
              <output_name>Advance</output_name>
              <incVal>0.5</incVal>
              <defaultValue>10</defaultValue>
              <input_0>0 1000 2000 3000 4000 5000 6000 7000 8000 9000 10000 11000 12000 13000 14000 15000</input_0>
              <input_1>
                <float>10</float><float>20</float><float>30</float><float>40</float>
                <float>50</float><float>60</float><float>70</float><float>80</float>
                <float>90</float><float>100</float><float>110</float><float>120</float>
                <float>130</float><float>140</float><float>150</float><float>160</float>
              </input_1>
              <output>0.0 1.5 -2.5 3.75</output>
            </TableModel>
            <TableModel>
              <id>11</id>
              <name>1D Table</name>
              <type>T1x16</type>
              <cols>16</cols>
              <rows>1</rows>
              <input_0_LinkId>1</input_0_LinkId>
              <output_LinkId>3</output_LinkId>
              <incVal>0.1</incVal>
            </TableModel>
          </tables>
          <drivers>
            <DriverModel>
              <id>5</id>
              <name>Boost Driver</name>
              <category>Boost</category>
              <ViewInTree>true</ViewInTree>
              <numberOfConfigs>2</numberOfConfigs>
              <configParams>
                <DriverModelParam>
                  <name>duty</name>
                  <DisplayName>Duty Cycle</DisplayName>
                  <SectionName>Output</SectionName>
                  <type>InputBox</type>
                  <readOnly>false</readOnly>
                  <RequiresReset>true</RequiresReset>
                  <value>50</value>
                  <min>0</min>
                  <max>100</max>
                  <CheckRange>true</CheckRange>
                  <ToolTipText>Duty in percent</ToolTipText>
                  <options>
                    <comboBoxOption><id>1</id><name>Off</name></comboBoxOption>
                    <comboBoxOption><id>2</id><name>On</name></comboBoxOption>
                  </options>
                  <ViewConstraint>
                    <ParamIndex>0</ParamIndex>
                    <AcceptedValues><float>1</float><float>2</float></AcceptedValues>
                  </ViewConstraint>
                </DriverModelParam>
                <DriverModelParam>
                  <name>legacy</name>
                  <Min>5</Min>
                  <Max>15</Max>
                  <type>InputBox</type>
                </DriverModelParam>
              </configParams>
              <numberOfOutputs>1</numberOfOutputs>
              <outputLinkIds><unsignedShort>3</unsignedShort></outputLinkIds>
              <editableOutputs>true</editableOutputs>
              <outputNames><string>Advance</string></outputNames>
              <numberOfInputs>2</numberOfInputs>
              <inputLinkIds>1 2</inputLinkIds>
              <editableInputs>false</editableInputs>
              <inputNames>RPM Load</inputNames>
            </DriverModel>
          </drivers>
        </ecu>
        """;

    [Fact]
    public void Parse_FullDef_ParsesMetadata()
    {
        var result = DefXmlParser.Parse(FullDefXml);

        result.Metadata.ProductName.Should().Be("ME221");
        result.Metadata.ModelName.Should().Be("V7 Test");
        result.Metadata.Version.Should().Be("1.2.3");
    }

    [Fact]
    public void Parse_FullDef_ParsesDataLinks()
    {
        var links = DefXmlParser.Parse(FullDefXml).DataLinks;

        links.Should().HaveCount(3);

        var rpm = links[0];
        rpm.Id.Should().Be(1);
        rpm.Name.Should().Be("Engine RPM");
        rpm.Category.Should().Be("Engine");
        rpm.ViewInTree.Should().BeTrue();
        rpm.StandardLogging.Should().BeTrue();
        rpm.MeasureUnit.Should().Be("RPM"); // explicit <measureUnit> wins
        rpm.MeasurementUnitTypes.Should().Be(MeasurementUnitType.Rpm);
        rpm.DataTypeSet.Should().Be(DataType.Normal);
        rpm.MinValue.Should().Be(0f);
        rpm.MaxValue.Should().Be(9000f);
        rpm.DataKey.Should().Be("rpm");
        rpm.TextValues.Should().BeEmpty();
        rpm.Feedbacks.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MeasureUnit_FallsBackToUnitTypeElementName()
    {
        var link = DefXmlParser.Parse(FullDefXml).DataLinks[1];

        link.MeasureUnit.Should().Be("°C");
        link.DataTypeSet.Should().Be(DataType.Percent);
        link.TextValues.Should().HaveCount(2);
        link.TextValues[0].Value.Should().Be(0f);
        link.TextValues[0].Text.Should().Be("Cold");
        link.TextValues[1].Text.Should().Be("Hot");
    }

    [Fact]
    public void Parse_Feedbacks_ParsesConstraintsAndFlashing()
    {
        var link = DefXmlParser.Parse(FullDefXml).DataLinks[1];
        link.Feedbacks.Should().HaveCount(3);

        var ok = link.Feedbacks[0];
        ok.Severity.Should().Be(DataLinkFeedbackSeverity.Ok);
        ok.MinValue.Should().Be(-40f);   // Gt
        ok.MaxValue.Should().Be(100f);   // Lt
        ok.Flashing.Should().BeNull();

        var warning = link.Feedbacks[1];
        warning.Severity.Should().Be(DataLinkFeedbackSeverity.Warning);
        warning.MinValue.Should().Be(100f);   // GtEq
        warning.MaxValue.Should().Be(120f);   // Lt
        warning.Flashing.Should().BeTrue();

        var alarm = link.Feedbacks[2];
        alarm.Severity.Should().Be(DataLinkFeedbackSeverity.Alarm);
        alarm.MinValue.Should().Be(120f); // GtEq
        // Eq constraint "3 4" contains a space — must be skipped, no range equivalent
        alarm.MaxValue.Should().BeNull();
        alarm.Flashing.Should().BeNull();
    }

    [Fact]
    public void Parse_UnknownUnitType_FallsBackToElementName()
    {
        var link = DefXmlParser.Parse(FullDefXml).DataLinks[2];

        link.MeasureUnit.Should().Be("CustomThing");
        link.MeasurementUnitTypes.Should().Be(MeasurementUnitType.Unknown);
    }

    [Fact]
    public void Parse_Tables_Parses2DTable()
    {
        var table = DefXmlParser.Parse(FullDefXml).Tables[0];

        table.Id.Should().Be(10);
        table.Name.Should().Be("Ignition Map");
        table.TableType.Should().Be("T16x16");
        table.Cols.Should().Be(16);
        table.Rows.Should().Be(16);
        table.Input0LinkId.Should().Be(1);
        table.Input1LinkId.Should().Be(2);
        table.OutputLinkId.Should().Be(3);
        table.Enabled.Should().BeTrue();
        table.IncrementValue.Should().Be(0.5f);
        table.DefaultValue.Should().Be(10f);
        table.Input0Name.Should().Be("RPM");
        table.Input1Name.Should().Be("Load");
        table.OutputName.Should().Be("Advance");

        table.Input0.Should().HaveCount(16);
        table.Input0![0].Should().Be(0f);
        table.Input0![15].Should().Be(15000f);

        // <float> child elements format
        table.Input1.Should().HaveCount(16);
        table.Input1![15].Should().Be(160f);

        // space-separated output
        table.Output.Should().HaveCount(4);
        table.Output![0].Should().Be(0f);
        table.Output![2].Should().Be(-2.5f);
        table.Output![3].Should().Be(3.75f);
    }

    [Fact]
    public void Parse_Tables_MissingInput1LinkDefaultsToZero()
    {
        var table = DefXmlParser.Parse(FullDefXml).Tables[1];

        table.TableType.Should().Be("T1x16");
        table.Input1LinkId.Should().Be(0);
        table.IncrementValue.Should().Be(0.1f);
        table.DefaultValue.Should().BeNull();
        table.Input0.Should().BeNull();
        table.Input1.Should().BeNull();
        table.Output.Should().BeNull();
    }

    [Fact]
    public void Parse_Drivers_ParsesParamsOptionsAndConstraints()
    {
        var driver = DefXmlParser.Parse(FullDefXml).Drivers[0];

        driver.Id.Should().Be(5);
        driver.Name.Should().Be("Boost Driver");
        driver.Category.Should().Be("Boost");
        driver.NumberOfConfigs.Should().Be(2);
        driver.Configs.Should().HaveCount(2);

        var duty = driver.Configs[0];
        duty.Name.Should().Be("duty");
        duty.DisplayName.Should().Be("Duty Cycle");
        duty.SectionName.Should().Be("Output");
        duty.ParamType.Should().Be("InputBox");
        duty.ReadOnly.Should().BeFalse();
        duty.RequiresReset.Should().BeTrue();
        duty.Value.Should().Be(50f);
        duty.Min.Should().Be(0f);
        duty.Max.Should().Be(100f);
        duty.CheckRange.Should().BeTrue();
        duty.ToolTipText.Should().Be("Duty in percent");
        duty.Options.Should().HaveCount(2);
        duty.Options[0].Id.Should().Be(1);
        duty.Options[0].Name.Should().Be("Off");
        duty.Options[1].Name.Should().Be("On");
        duty.ViewConstraint.Should().NotBeNull();
        duty.ViewConstraint!.ParamIndex.Should().Be(0);
        duty.ViewConstraint.AcceptedValues.Should().Equal([1f, 2f]);

        // legacy <Min>/<Max> casing fallback
        var legacy = driver.Configs[1];
        legacy.Min.Should().Be(5f);
        legacy.Max.Should().Be(15f);

        driver.NumberOfOutputs.Should().Be(1);
        driver.OutputLinkIds.Should().Equal([3]);
        driver.EditableOutputs.Should().BeTrue();
        driver.OutputNames.Should().Equal(["Advance"]);
        driver.NumberOfInputs.Should().Be(2);
        driver.InputLinkIds.Should().Equal([1, 2]); // space-separated fallback
        driver.EditableInputs.Should().BeFalse();
        driver.InputNames.Should().Equal(["RPM", "Load"]); // space-separated fallback
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsDefaults()
    {
        var result = DefXmlParser.Parse("<ecu />");

        result.Metadata.ProductName.Should().Be("");
        result.DataLinks.Should().BeEmpty();
        result.Tables.Should().BeEmpty();
        result.Drivers.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingSections_AreIgnored()
    {
        var result = DefXmlParser.Parse("<ecu><links /></ecu>");

        result.DataLinks.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedXml_Throws()
    {
        var act = () => DefXmlParser.Parse("<ecu><links>");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_EmptyDocument_ThrowsXmlError()
    {
        // XDocument.Parse rejects documents without a root before the parser's
        // own guard can run — surfaces as an XML parse exception.
        var act = () => DefXmlParser.Parse("   ");

        act.Should().Throw<System.Xml.XmlException>();
    }

    [Fact]
    public void Parse_WrongRootName_IsToleratedAsEmpty()
    {
        // The parser only validates that a root element EXISTS, not its name
        var result = DefXmlParser.Parse("<notAnEcu />");

        result.DataLinks.Should().BeEmpty();
        result.Tables.Should().BeEmpty();
        result.Drivers.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Feedbacks_UnknownSeverityElement_IsSkipped()
    {
        const string xml = """
            <ecu>
              <links>
                <DataLinkModel>
                  <id>1</id>
                  <Feedbacks>
                    <Ok />
                    <MysterySeverity />
                  </Feedbacks>
                </DataLinkModel>
              </links>
            </ecu>
            """;

        var link = DefXmlParser.Parse(xml).DataLinks.Single();

        link.Feedbacks.Should().ContainSingle();
        link.Feedbacks[0].Severity.Should().Be(DataLinkFeedbackSeverity.Ok);
    }

    [Fact]
    public void Parse_ViewConstraint_SpaceSeparatedAcceptedValues_FallbackWorks()
    {
        const string xml = """
            <ecu>
              <drivers>
                <DriverModel>
                  <id>1</id>
                  <configParams>
                    <DriverModelParam>
                      <name>p</name>
                      <ViewConstraint>
                        <ParamIndex>2</ParamIndex>
                        <AcceptedValues>5 6 7</AcceptedValues>
                      </ViewConstraint>
                    </DriverModelParam>
                  </configParams>
                </DriverModel>
              </drivers>
            </ecu>
            """;

        var param = DefXmlParser.Parse(xml).Drivers.Single().Configs.Single();

        param.ViewConstraint.Should().NotBeNull();
        param.ViewConstraint!.ParamIndex.Should().Be(2);
        param.ViewConstraint.AcceptedValues.Should().Equal([5f, 6f, 7f]);
    }

    [Fact]
    public void Parse_ViewConstraint_MissingAcceptedValues_ReturnsNull()
    {
        const string xml = """
            <ecu>
              <drivers>
                <DriverModel>
                  <id>1</id>
                  <configParams>
                    <DriverModelParam>
                      <name>p</name>
                      <ViewConstraint>
                        <ParamIndex>2</ParamIndex>
                      </ViewConstraint>
                    </DriverModelParam>
                  </configParams>
                </DriverModel>
              </drivers>
            </ecu>
            """;

        var param = DefXmlParser.Parse(xml).Drivers.Single().Configs.Single();

        param.ViewConstraint.Should().BeNull();
    }

    [Fact]
    public void Parse_Table_DefaultTypeIsT16x16()
    {
        const string xml = """
            <ecu>
              <tables>
                <TableModel>
                  <id>1</id>
                  <cols>16</cols>
                  <rows>16</rows>
                  <input_0_LinkId>1</input_0_LinkId>
                  <output_LinkId>3</output_LinkId>
                </TableModel>
              </tables>
            </ecu>
            """;

        var table = DefXmlParser.Parse(xml).Tables.Single();

        table.TableType.Should().Be("T16x16");
        table.ViewInTree.Should().BeFalse();
        table.Enabled.Should().BeFalse();
        table.IncrementValue.Should().Be(0.1f);
    }
}
