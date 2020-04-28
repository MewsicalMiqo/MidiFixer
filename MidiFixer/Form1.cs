using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".mid"))
                {
                    LoadMIDIFromFile(files[i]);
                    break;
                }
            }
        }

        void LoadMIDIFromFile(string path)
        {
            song = MidiFile.Read(path);
            filePath = path;
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
            openFileDialog.Filter = "MIDI files (*.mid)|*.mid";
            openFileDialog.RestoreDirectory = true;

            string filePath = default;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
                LoadMIDIFromFile(filePath);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var chunks = song.GetTrackChunks();
            var tempoMap = song.GetTempoMap();
            var channels = song.GetChannels();

            var fixedMidi = new MidiFile();
            var headerChunk = new TrackChunk();

            var headerEvents = chunks.ElementAt(0).GetTimedEvents();
            var titleEvent = new List<TimedEvent>();
            foreach (var evt in headerEvents)
            {
                if (evt.Event.EventType == MidiEventType.SequenceTrackName)
                {
                    var txtEvent = evt.Event as SequenceTrackNameEvent;
                    titleEvent.Add(evt);
                    break;
                }
            }
            headerChunk.AddTimedEvents(titleEvent);

            fixedMidi.Chunks.Add(headerChunk);
            fixedMidi.ReplaceTempoMap(tempoMap);

            var notes = song.GetNotes();
            for (int i = 0; i < channels.Count(); i++)
            {
                var trackNotes = notes.Where(n => n.Channel == i);
                if (trackNotes.Count() > 0)
                {
                    var trackChunk = new TrackChunk();
                    trackChunk.AddNotes(trackNotes);
                    var newTimedEvents = GetTimedEventsByChannel(chunks, i);
                    trackChunk.AddTimedEvents(newTimedEvents);
                    fixedMidi.Chunks.Add(trackChunk);
                }
            }
            
            string outputFilename = Path.GetFileNameWithoutExtension(filePath) + "_fix.mid";
            WriteMidiToFile(fixedMidi, outputFilename);
            textBox1.AppendText("Wrote to file: " + outputFilename + Environment.NewLine);
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

        private List<TimedEvent> GetTimedEventsByChannel(IEnumerable<TrackChunk> chunks, int ch)
        {
            var timedEvents = chunks.GetTimedEvents();
            var newTimedEvents = new List<TimedEvent>();

            foreach (var timedEvent in timedEvents)
            {
                var pc = timedEvent.Event as ProgramChangeEvent;
                if (pc != null && pc.Channel == ch)
                    newTimedEvents.Add(timedEvent);
            }            
            foreach (var c in chunks)
            {
                var channels = c.GetChannels();
                if (channels.Contains((FourBitNumber)ch))
                {
                    var textEvents = c.GetTimedEvents().Where(evt => evt.Event.EventType == MidiEventType.SequenceTrackName);
                    if (textEvents.Count() == 0)
                        continue;
                    else
                        foreach (var te in textEvents)
                            newTimedEvents.Add(te);
                }
            }
            return newTimedEvents;
        }
    }
}
