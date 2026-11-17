<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="utf-8" indent="yes"/>

  <xsl:template match="/IndividualWork">
    <html lang="uk">
      <head>
        <title>Індивідуальна робота кафедри зі студентами</title>
        <style>
          body { font-family: "Segoe UI", Arial, sans-serif; margin: 16px; color: #1f2933; background: #ffffff; }
          h1 { font-size: 20px; margin: 0 0 4px 0; }
          p.meta { margin: 0 0 12px 0; color: #52606d; }
          table { border-collapse: collapse; width: 100%; font-size: 13px; }
          th, td { border: 1px solid #cbd2d9; padding: 6px 8px; vertical-align: top; text-align: left; }
          th { background: #e4e7eb; }
          tr:nth-child(even) td { background: #f5f7fa; }
          ul { margin: 0; padding-left: 18px; }
          .empty { color: #9aa5b1; }
          .answer { color: #52606d; }
        </style>
      </head>
      <body>
        <h1>Індивідуальна робота кафедри зі студентами</h1>
        <p class="meta">
          <xsl:value-of select="@faculty"/>
          <xsl:text>, </xsl:text>
          <xsl:value-of select="@department"/>
          <xsl:if test="@year">
            <xsl:text>, </xsl:text>
            <xsl:value-of select="@year"/>
          </xsl:if>
          <xsl:text>. Записів: </xsl:text>
          <xsl:value-of select="count(Work)"/>
        </p>
        <table>
          <tr>
            <th>№</th>
            <th>Студент</th>
            <th>Група</th>
            <th>Викладач</th>
            <th>Тема</th>
            <th>Графік</th>
            <th>Статус</th>
            <th>Оцінка</th>
            <th>Завдання</th>
            <th>Матеріали</th>
            <th>Запитання та відповіді</th>
          </tr>
          <xsl:apply-templates select="Work"/>
        </table>
      </body>
    </html>
  </xsl:template>

  <xsl:template match="Work">
    <tr>
      <td><xsl:value-of select="@id"/></td>
      <td><xsl:value-of select="@student"/></td>
      <td><xsl:value-of select="@group"/></td>
      <td>
        <xsl:value-of select="Teacher"/>
        <xsl:if test="Teacher/@position">
          <xsl:text> (</xsl:text>
          <xsl:value-of select="Teacher/@position"/>
          <xsl:text>)</xsl:text>
        </xsl:if>
      </td>
      <td><xsl:value-of select="Topic"/></td>
      <td>
        <xsl:value-of select="Schedule/@day"/>
        <xsl:text> </xsl:text>
        <xsl:value-of select="Schedule/@time"/>
        <xsl:if test="@room">
          <xsl:text>, ауд. </xsl:text>
          <xsl:value-of select="@room"/>
        </xsl:if>
        <xsl:if test="@consultationType">
          <xsl:text>, </xsl:text>
          <xsl:value-of select="@consultationType"/>
        </xsl:if>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="@status"><xsl:value-of select="@status"/></xsl:when>
          <xsl:otherwise><span class="empty">—</span></xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="@grade"><xsl:value-of select="@grade"/></xsl:when>
          <xsl:otherwise><span class="empty">—</span></xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="Tasks/Task">
            <ul>
              <xsl:for-each select="Tasks/Task">
                <li>
                  <xsl:value-of select="."/>
                  <xsl:text> — до </xsl:text>
                  <xsl:value-of select="@deadline"/>
                  <xsl:text>, </xsl:text>
                  <xsl:value-of select="@status"/>
                </li>
              </xsl:for-each>
            </ul>
          </xsl:when>
          <xsl:otherwise><span class="empty">—</span></xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="Materials/Material">
            <ul>
              <xsl:for-each select="Materials/Material">
                <li>
                  <xsl:choose>
                    <xsl:when test="@url">
                      <a href="{@url}"><xsl:value-of select="."/></a>
                    </xsl:when>
                    <xsl:otherwise><xsl:value-of select="."/></xsl:otherwise>
                  </xsl:choose>
                  <xsl:if test="@type">
                    <xsl:text> (</xsl:text>
                    <xsl:value-of select="@type"/>
                    <xsl:text>)</xsl:text>
                  </xsl:if>
                </li>
              </xsl:for-each>
            </ul>
          </xsl:when>
          <xsl:otherwise><span class="empty">—</span></xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="Questions/Question">
            <ul>
              <xsl:for-each select="Questions/Question">
                <li>
                  <xsl:value-of select="Text"/>
                  <xsl:choose>
                    <xsl:when test="Answer">
                      <div class="answer">Відповідь: <xsl:value-of select="Answer"/></div>
                    </xsl:when>
                    <xsl:otherwise>
                      <div class="empty">Відповіді ще немає</div>
                    </xsl:otherwise>
                  </xsl:choose>
                </li>
              </xsl:for-each>
            </ul>
          </xsl:when>
          <xsl:otherwise><span class="empty">—</span></xsl:otherwise>
        </xsl:choose>
      </td>
    </tr>
  </xsl:template>
</xsl:stylesheet>
