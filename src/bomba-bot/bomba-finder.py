# bot.py
import re
import sqlite3
import os
from dotenv import load_dotenv
from rapidfuzz import process
from collections import defaultdict
from datetime import timedelta

import discord
from discord import app_commands

class BombaSubtitles:
  def __init__(self, id, title, videoUrl, subtitles, offset):
    self.id = id
    self.title = title
    self.videoUrl = videoUrl
    self.subtitles = subtitles
    self.offset = offset

  def __repr__(self):
    return f"BombaSubtitles(id={self.id}, title={self.title}, videoUrl={self.videoUrl}, subtitles={self.subtitles}, offset={self.offset})"

def get_all_subtitles_from_db() -> list[BombaSubtitles]:
    # Connect to the SQLite database
    conn = sqlite3.connect('bomba.db')
    cursor = conn.cursor()
    
    # Query to select all rows from the BombaSubtitles table
    query = "SELECT id, title, videoUrl, subtitles, offset FROM BombaSubtitles ORDER BY offset"
    cursor.execute(query)
    
    # Fetch all results
    rows = cursor.fetchall()
    
    # List to hold BombaSubtitles objects
    subtitles_list = []
    
    # Iterate over each row and create BombaSubtitles objects
    for row in rows:
        id, title, videoUrl, subtitles, offset = row
        subtitle = BombaSubtitles(id, title, videoUrl, subtitles.lower(), offset)
        subtitles_list.append(subtitle)
    
    # Close the connection
    conn.close()
    
    return subtitles_list

def parse_offset(offset_str):
    # Parse offset string with milliseconds (example format: "01:23:45.678")
    parts = offset_str.split(':')
    hours = int(parts[0])
    minutes = int(parts[1])

    if '.' not in parts[2]:
        parts[2] = parts[2] + '.000'
	
    splitted_last_part = parts[2].split('.')
    splitted_last_part[1] = splitted_last_part[1][:3]
    seconds, milliseconds = map(int, splitted_last_part)
    return timedelta(hours=hours, minutes=minutes, seconds=seconds, milliseconds=milliseconds)

def get_subtitles_sliding_windows(subtitles, quote) -> list[BombaSubtitles]:
    # group subtitles by video
    subtitles_by_video = defaultdict(list)
    n = len(quote)
    for subtitle in subtitles:
        subtitles_by_video[subtitle.videoUrl].append(subtitle)

    subtitle_groups = []
    for videoUrl in subtitles_by_video:
    
        reached_last_subtitle = False
        for i in range(len(subtitles_by_video[videoUrl])):
            # for each subtitle, add next subtitles until added at least n characters (with spaces)
            sum = 0
            subtitle_group = subtitles_by_video[videoUrl][i].subtitles
            offset = subtitles_by_video[videoUrl][i].offset

            for subtitle in subtitles_by_video[videoUrl][i+1:]:
                # if we reached last subtitles we want to stop creating new windows
                if reached_last_subtitle == False:
                    sum = sum + len(subtitle.subtitles) + 1
                    subtitle_group = subtitle_group + ' ' + subtitle.subtitles

                    # if we reached last subtitle
                    if subtitle == subtitles_by_video[videoUrl][-1]:
                        reached_last_subtitle = True
                        break
                    
                    # if already added n characters we can stop
                    if sum > n:
                        break

            subtitle_groups.append(BombaSubtitles(subtitle.id, subtitle.title, subtitle.videoUrl, subtitle_group, offset))

    return subtitle_groups

def search_quote_in_db(quote):
    subtitles_list = get_all_subtitles_from_db()

    sliding_window_subtitles = get_subtitles_sliding_windows(subtitles_list, quote)
    subtitles_list.extend(sliding_window_subtitles)

    contents = [subtitle.subtitles for subtitle in subtitles_list]

    best_match, confidence, key = process.extractOne(quote, contents)
    for row in subtitles_list:
        if row.subtitles == best_match:
            best_row = row
            break

    print(f'Found |{quote}| in {best_row.title} with confidence {confidence}')

    title = best_row.title
    video_url = best_row.videoUrl

    return { 'Title': title, 'VideoUrl': video_url, 'Timestamp': best_row.offset, 'Confidence': confidence }

# START

load_dotenv()
discord_token = os.getenv('DISCORD_TOKEN').encode('utf-8').decode('unicode_escape')

if not discord_token:
    raise ValueError("No DISCORD_TOKEN found in environment variables")

intents = discord.Intents.default()
intents.message_content = True

client = discord.Client(intents=intents)

tree = app_commands.CommandTree(client)

@tree.command(name='bomba', description='Podaj cytat')
async def bomba(interaction: discord.Interaction, text: str):
    print(f'Received bomba command with text: {text}')
    result = search_quote_in_db(text.lower())
    print(f"Best result is {result['Title']} with confidence {result['Confidence']}")

    embed = discord.Embed(
        title=f'Szukam "{text}"',
        description=f'Wynik znaleziono z dopasowaniem {result["Confidence"]:.1f}%',
        color=discord.Color.blue()
    )

    # set thumbnail
    video_id = re.search(r'(?:v=|\/)([0-9A-Za-z_-]{11}).*', result['VideoUrl']).group(1)
    thumbnail_url = f'https://img.youtube.com/vi/{video_id}/hqdefault.jpg'
    embed.set_thumbnail(url=thumbnail_url)

    timestamp_seconds = parse_offset(result['Timestamp']).seconds
    embed.add_field(
        name=result['Title'],
        value=f"[{result['Title']}]({result['VideoUrl']}&t={timestamp_seconds})",
        inline=True
    )
    await interaction.response.send_message(embed=embed)

@client.event
async def on_ready():
    await tree.sync()
    print('Logged in successfully')

client.run(discord_token)
