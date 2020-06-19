using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiFixer
{
    public partial class Form1 : Form
    {
        MidiFile song;
        string filePath;
        StringBuilder sb;

        public Form1()
        {
            InitializeComponent();
        }

        void FormDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        void FormDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files[0].EndsWith(".mid"))
                LoadMIDIFile(files[0]);
            else if (files[0].EndsWith(".mml"))
                LoadMMLFile(files[0]);
        }

        void LoadMMLFile(string path)
        {
            filePath = path;
            string _importedMML = File.ReadAllText(path);
            _importedMML = _importedMML.Remove(0, 4);  // removes MML@
            _importedMML = _importedMML.Trim(new char[] { ';' });
            var _MMLTracks = _importedMML.Split(',');
            textBox1.Clear();

            sb = new StringBuilder();
            string channelHeader;
            for (int ch = 1, chunk = _MMLTracks.Length - 1; ch < _MMLTracks.Length + 1; ch++, chunk--)
            {
                channelHeader = "[Channel" + ch.ToString() + "]";
                sb.Append(channelHeader + Environment.NewLine);
                sb.Append(_MMLTracks[chunk] + Environment.NewLine);                
            }
            textBox1.AppendText(sb.ToString());
        }

        void LoadMIDIFile(string path)
        {
            filePath = path;
            song = MidiFile.Read(path);
            textBox1.Text = "MIDI File: " + Path.GetFileName(path) + Environment.NewLine;
            textBox1.AppendText("Type: " + song.OriginalFormat + Environment.NewLine);
            textBox1.AppendText("Channels: " + song.GetChannels().Count() + Environment.NewLine);
            ReadTracksFromFile(song);
        }

        private void WriteMidiToFile(MidiFile inputFile, string fileName)
        {
            inputFile.Write(fileName, true, MidiFileFormat.MultiTrack,
               new WritingSettings { CompressionPolicy = CompressionPolicy.Default });
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "MIDI files (*.mid)|*.mid|MML files (*.mml)|*.mml";
            openFileDialog.RestoreDirectory = true;

            string filePath = default;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
                if (filePath.EndsWith(".mid"))
                    LoadMIDIFile(filePath);
                else if (filePath.EndsWith(".mml"))
                    LoadMMLFile(filePath);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (Path.GetExtension(filePath) == ".mid")
            {
                var chunks = song.GetTrackChunks().ToArray();
                var tempoMap = song.GetTempoMap();
                var fixedMidi = new MidiFile();
                var headerChunk = new TrackChunk();

                int i = 0;
                var t0Notes = chunks[0].GetNotes().ToArray();
                if (t0Notes.Length == 0)    // check if there are notes in Track0
                {
                    i = 1;
                    var timedEvents = chunks[0].GetTimedEvents().ToArray();
                    var title = timedEvents.Where(t => t.Event.EventType == MidiEventType.SequenceTrackName);
                    if (title.Count() == 1)
                        headerChunk.AddTimedEvents(title);
                }

                fixedMidi.Chunks.Add(headerChunk);
                fixedMidi.ReplaceTempoMap(tempoMap);

                for (int ch = 0; i < chunks.Count(); i++, ch++)
                {
                    var newChunk = new TrackChunk();
                    var timedEvents = chunks[i].GetTimedEvents();

                    using (var timedEventsManager = newChunk.ManageTimedEvents())
                    {
                        foreach (var timedEvent in timedEvents)
                        {
                            var eventType = timedEvent.Event.EventType;
                            if (eventType == MidiEventType.SequenceTrackName)
                            {
                                timedEventsManager.Events.Add(timedEvent);
                            }
                            else if (eventType == MidiEventType.ProgramChange)
                            {
                                var pcEvent = timedEvent.Event as ProgramChangeEvent;
                                pcEvent.Channel = (FourBitNumber)ch;
                                timedEventsManager.Events.AddEvent(pcEvent, timedEvent.Time);
                            }
                        }
                    }

                    using (var notesManager = newChunk.ManageNotes())
                    {
                        var notes = chunks[i].GetNotes().ToArray();
                        foreach (var n in notes)
                        {
                            var newNote = n;
                            newNote.Channel = (FourBitNumber)ch;   // change channel of the notes in track
                            notesManager.Notes.Add(newNote);
                        }
                    }

                    fixedMidi.Chunks.Add(newChunk);
                }

                string outputFilename = Path.GetFileNameWithoutExtension(filePath) + "_fix.mid";
                WriteMidiToFile(fixedMidi, outputFilename);
                textBox1.AppendText("Wrote to file: " + outputFilename + Environment.NewLine);
            }
            else if (Path.GetExtension(filePath) == ".mml")
            {
                string outputFilename = Path.GetFileNameWithoutExtension(filePath) + "_fix.mml";
                File.WriteAllText(outputFilename, sb.ToString());
                textBox1.AppendText("Wrote to file: " + outputFilename + Environment.NewLine);
            }
        }

        private void ReadTracksFromFile(MidiFile song)
        {
            var chunks = song.GetTrackChunks();

            for (int i = 0; i < chunks.Count(); i++)
            {
                textBox1.AppendText("Track " + i + ": ");
                var channels = chunks.ElementAt(i).GetChannels();
                foreach (var s in channels)
                    textBox1.AppendText("ch" + s + " ");
                textBox1.AppendText(Environment.NewLine);
            }
        }
    }
}
