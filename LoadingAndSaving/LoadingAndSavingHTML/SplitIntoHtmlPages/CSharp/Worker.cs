//////////////////////////////////////////////////////////////////////////
// Copyright 2001-2014 Aspose Pty Ltd. All Rights Reserved.
//
// This file is part of Aspose.Words. The source code in this file
// is only intended as a supplement to the documentation, and is provided
// "as is", without warranty of any kind, either expressed or implied.
//////////////////////////////////////////////////////////////////////////
using System.Collections;
using System.IO;
using System.Text;
using Aspose.Words;
using Aspose.Words.Saving;
using Aspose.Words.MailMerging;

namespace SplitIntoHtmlPagesExample
{
    /// <summary>
    /// This class takes a Microsoft Word document, splits it into topics at paragraphs formatted
    /// with the Heading 1 style and saves every topic as an HTML file.
    /// 
    /// Also generates contents.html file that provides links to all saved topics.
    /// </summary>
    internal class Worker
    {
        /// <summary>
        /// Performs the Word to HTML conversion.
        /// </summary>
        /// <param name="srcFileName">The MS Word file to convert.</param>
        /// <param name="tocTemplate">An MS Word file that is used as a template to build
        /// a table of contents. This file needs to have a mail merge region called "TOC" defined
        /// and one mail merge field called "TocEntry".</param>
        /// <param name="dstDir">The output directory where to write HTML files. Must exist.</param>
        internal void Execute(string srcFileName, string tocTemplate, string dstDir)
        {
            mDoc = new Document(srcFileName);
            mTocTemplate = tocTemplate;
            mDstDir = dstDir;

            ArrayList topicStartParas = SelectTopicStarts();
            InsertSectionBreaks(topicStartParas);
            ArrayList topics = SaveHtmlTopics();
            SaveTableOfContents(topics);
        }

        /// <summary>
        /// Selects heading paragraphs that must become topic starts.
        /// We can't modify them in this loop, we have to remember them in an array first.
        /// </summary>
        private ArrayList SelectTopicStarts()
        {
            NodeCollection paras = mDoc.GetChildNodes(NodeType.Paragraph, true, false);
            ArrayList topicStartParas = new ArrayList();

            foreach (Paragraph para in paras)
            {
                StyleIdentifier style = para.ParagraphFormat.StyleIdentifier;
                if (style == StyleIdentifier.Heading1)
                    topicStartParas.Add(para);
            }

            return topicStartParas;
        }

        /// <summary>
        /// Inserts section breaks before the specified paragraphs.
        /// </summary>
        private void InsertSectionBreaks(ArrayList topicStartParas)
        {
            DocumentBuilder builder = new DocumentBuilder(mDoc);
            foreach (Paragraph para in topicStartParas)
            {
                Section section = para.ParentSection;

                // Insert section break if the paragraph is not at the beginning of a section already.
                if (para != section.Body.FirstParagraph)
                {
                    builder.MoveTo(para.FirstChild);
                    builder.InsertBreak(BreakType.SectionBreakNewPage);

                    // This is the paragraph that was inserted at the end of the now old section.
                    // We don't really need the extra paragraph, we just needed the section.
                    section.Body.LastParagraph.Remove();
                }
            }
        }

        /// <summary>
        /// Splits the current document into one topic per section and saves each topic
        /// as an HTML file. Returns a collection of Topic objects.
        /// </summary>
        private ArrayList SaveHtmlTopics()
        {
            ArrayList topics = new ArrayList();
            for (int sectionIdx = 0; sectionIdx < mDoc.Sections.Count; sectionIdx++)
            {
                Section section = mDoc.Sections[sectionIdx];

                string paraText = section.Body.FirstParagraph.GetText();

                // The text of the heading paragaph is used to generate the HTML file name.
                string fileName = MakeTopicFileName(paraText);
                if (fileName == "")
                    fileName = "UNTITLED SECTION " + sectionIdx;

                fileName = Path.Combine(mDstDir, fileName + ".html");

                // The text of the heading paragraph is also used to generate the title for the TOC.
                string title = MakeTopicTitle(paraText);
                if (title == "")
                    title = "UNTITLED SECTION " + sectionIdx;

                Topic topic = new Topic(title, fileName);
                topics.Add(topic);

                SaveHtmlTopic(section, topic);
            }

            return topics;
        }

        /// <summary>
        /// Leaves alphanumeric characters, replaces white space with underscore
        /// and removes all other characters from a string.
        /// </summary>
        private static string MakeTopicFileName(string paraText)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in paraText)
            {
                if (char.IsLetterOrDigit(c))
                    b.Append(c);
                else if (c == ' ')
                    b.Append('_');
            }
            return b.ToString();
        }

        /// <summary>
        /// Removes the last character (which is a paragraph break character from the given string).
        /// </summary>
        private static string MakeTopicTitle(string paraText)
        {
            return paraText.Substring(0, paraText.Length - 1);
        }

        /// <summary>
        /// Saves one section of a document as an HTML file.
        /// Any embedded images are saved as separate files in the same folder as the HTML file.
        /// </summary>
        private static void SaveHtmlTopic(Section section, Topic topic)
        {
            Document dummyDoc = new Document();
            dummyDoc.RemoveAllChildren();
            dummyDoc.AppendChild(dummyDoc.ImportNode(section, true, ImportFormatMode.KeepSourceFormatting));

            dummyDoc.BuiltInDocumentProperties.Title = topic.Title;

            HtmlSaveOptions saveOptions = new HtmlSaveOptions();
            saveOptions.PrettyFormat = true;
            // This is to allow headings to appear to the left of main text.
            saveOptions.AllowNegativeLeftIndent = true;
            saveOptions.ExportHeadersFootersMode = ExportHeadersFootersMode.None;

            dummyDoc.Save(topic.FileName, saveOptions);
        }

        /// <summary>
        /// Generates a table of contents for the topics and saves to contents.html.
        /// </summary>
        private void SaveTableOfContents(ArrayList topics)
        {
            Document tocDoc = new Document(mTocTemplate);
            
            // We use a custom mail merge even handler defined below.
            tocDoc.MailMerge.FieldMergingCallback = new HandleTocMergeField();
            // We use a custom mail merge data source based on the collection of the topics we created.
            tocDoc.MailMerge.ExecuteWithRegions(new TocMailMergeDataSource(topics));
            
            tocDoc.Save(Path.Combine(mDstDir, "contents.html"));
        }

        private class HandleTocMergeField : IFieldMergingCallback
        {
            void IFieldMergingCallback.FieldMerging(FieldMergingArgs e)
            {
                if (mBuilder == null)
                    mBuilder = new DocumentBuilder(e.Document);

                // Our custom data source returns topic objects.
                Topic topic = (Topic)e.FieldValue;

                // We use the document builder to move to the current merge field and insert a hyperlink.
                mBuilder.MoveToMergeField(e.FieldName);
                mBuilder.InsertHyperlink(topic.Title, topic.FileName, false);

                // Signal to the mail merge engine that it does not need to insert text into the field
                // as we've done it already.
                e.Text = "";
            }

            void IFieldMergingCallback.ImageFieldMerging(ImageFieldMergingArgs args)
            {
                // Do nothing.
            }

            private DocumentBuilder mBuilder;
        }

        private Document mDoc;
        private string mTocTemplate;
        private string mDstDir;
    }
}